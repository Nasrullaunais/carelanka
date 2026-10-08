import { randomBytes, randomInt } from 'node:crypto';
import { expect, test, type Browser, type Page } from '@playwright/test';

// The local Docker stack only. Never the hosted server.
const WEB_URL = 'http://localhost:5174';
const API_URL = 'http://localhost:5231/api';

// Seeded demo staff logins from TEST_ACCOUNTS.md.
const RECEPTION = { email: 'staff.jayasuriya@carelanka.lk', password: 'CareLanka#2026' };
const NURSE = { email: 'nurse.perera@carelanka.lk', password: 'CareLanka#2026' };

const QUESTION = 'I have a mild headache since this morning.';
const NURSE_NOTE = 'A nurse will check on you within the next hour.';

// A brand new patient on every run, so the test can be run again and again.
const id = `${Date.now().toString(36)}${randomBytes(2).toString('hex')}`;
const randomDigits = (count: number) => Array.from({ length: count }, () => randomInt(10)).join('');
const patient = {
  fullName: `QM Test E2E ${id}`,
  nic: `1990${randomDigits(8)}`, // a 12-digit NIC starts with the birth year
  phone: `07${randomDigits(8)}`,
  username: `qm-e2e-${id}`,
  password: `E2e#${randomBytes(9).toString('base64url')}`,
};

// Opens a new browser window and signs in. Grouped as one step so the report never shows the password.
async function signIn(browser: Browser, who: { email: string; password: string }) {
  const page = await (await browser.newContext({ viewport: { width: 1440, height: 900 } })).newPage();
  await test.step(`Sign in to the web app as ${who.email}`, async () => {
    await page.goto(`${WEB_URL}/login`);
    await page.getByLabel('Email').fill(who.email);
    await page.getByLabel('Password').fill(who.password);
    await page.getByRole('button', { name: 'Sign in' }).click();
    await expect(page).not.toHaveURL(/\/login/);
  }, { box: true });
  return page;
}

// Picks an option from one of the app's dropdowns.
async function choose(page: Page, dropdown: string, option: string) {
  await page.getByRole('button', { name: dropdown }).click();
  await page.getByRole('option', { name: option, exact: true }).click();
}

async function screenshot(page: Page, name: string) {
  await page.screenshot({ path: `results/screens/${name}.png`, fullPage: true, animations: 'disabled' });
}

test('E2E-01: walk-in patient is admitted, asks the care agent, and reads the nurse-approved reply', async ({
  browser,
  request,
}) => {
  let patientCode = '';
  let patientLogin = {};
  let bed = '';
  let workflowId = '';
  let recommendationId = '';
  let agentDraft = '';
  let approvedText = '';

  const reception = await signIn(browser, RECEPTION);

  await test.step('1. Reception registers a new walk-in patient (web)', async () => {
    await reception.goto(`${WEB_URL}/intake`);
    await reception.getByLabel('NIC').fill(patient.nic);
    await reception.getByRole('button', { name: 'Search' }).click();
    await expect(reception.getByRole('heading', { name: 'No record for that NIC' })).toBeVisible();
    await reception.getByRole('button', { name: 'Register a new patient' }).click();
    await reception.getByRole('button', { name: /General ward - normal admission/ }).click();

    await reception.getByLabel('Full name').fill(patient.fullName);
    await choose(reception, 'Gender', 'Male');
    await choose(reception, 'Day of birth', '15');
    await choose(reception, 'Month of birth', 'June');
    await choose(reception, 'Year of birth', '1990');
    await reception.getByLabel('Phone', { exact: true }).fill(patient.phone);
    await reception.getByLabel('Address').fill('12 Test Lane, Colombo');
    await reception.getByRole('button', { name: 'Register and continue' }).click();

    // The pop-up message gives the new patient code, e.g. "Patient ID PVAVKCPR."
    const message = reception.getByText(`${patient.fullName} registered. Patient ID`);
    await expect(message).toBeVisible();
    patientCode = /Patient ID (\w+)\./.exec((await message.textContent()) ?? '')![1];
    expect(patientCode).toHaveLength(8);
  });

  await test.step('2. Reception admits the patient (web)', async () => {
    await reception.getByRole('button', { name: 'Admit patient' }).click();
    await expect(reception.getByText(`${patient.fullName} is admitted and awaiting a bed.`)).toBeVisible();
    await expect(reception.locator('code', { hasText: patientCode })).toBeVisible();
    await screenshot(reception, 'PT-E2E-01-step2-admitted-awaiting-bed');
  });

  await test.step('3. Patient signs up and signs in (API, as the mobile app)', async () => {
    const signUp = await request.post(`${API_URL}/auth/patient/register`, {
      data: { username: patient.username, password: patient.password },
    });
    expect(signUp.ok()).toBe(true);

    const login = await request.post(`${API_URL}/auth/patient/login`, {
      data: { username: patient.username, password: patient.password },
    });
    expect(login.status()).toBe(200);
    patientLogin = { Authorization: `Bearer ${(await login.json()).access_token}` };
  });

  await test.step('4. Patient links the hospital record with the patient code and NIC (API)', async () => {
    const codeAndNic = { patient_code: patientCode, nic: patient.nic };

    const preview = await request.post(`${API_URL}/me/claim/preview`, { headers: patientLogin, data: codeAndNic });
    expect(preview.status()).toBe(200);
    expect((await preview.json()).masked_full_name).not.toBe(patient.fullName); // the name is partly hidden

    const claim = await request.post(`${API_URL}/me/claim`, { headers: patientLogin, data: codeAndNic });
    expect(claim.status()).toBe(200);

    const profile = await request.get(`${API_URL}/me/profile`, { headers: patientLogin });
    expect(await profile.json()).toMatchObject({ patient_code: patientCode, full_name: patient.fullName, nic: patient.nic });
  });

  await test.step('5. Negative: a care question before the patient has a bed is refused (API)', async () => {
    const tooEarly = await request.post(`${API_URL}/me/care-queries`, {
      headers: patientLogin,
      data: { reported_text: QUESTION },
    });
    expect(tooEarly.status()).toBe(409);
    expect((await tooEarly.json()).code).toBe('cl_pat_038'); // "not currently admitted"
  });

  const nurse = await signIn(browser, NURSE);

  await test.step('6. Ward nurse assigns a bed, which admits the walk-in (web)', async () => {
    await nurse.goto(`${WEB_URL}/patients`);
    await nurse.getByLabel('Search').fill(patientCode);
    await nurse.getByRole('button', { name: 'Search', exact: true }).click();

    const row = nurse.getByRole('row').filter({ hasText: patientCode });
    await expect(row).toContainText('Awaiting bed');
    await row.getByRole('button', { name: 'Assign bed' }).click();

    // Any free bed in the men's general medical ward.
    await nurse
      .getByRole('dialog')
      .getByRole('row')
      .filter({ hasText: 'General Medical Ward (Male)' })
      .getByRole('button', { name: 'Assign and admit', exact: true })
      .first()
      .click();

    // The pop-up message names the bed, e.g. "... is in General Medical Ward (Male) · GMM-01."
    const message = nurse.getByText(`${patient.fullName} is in General Medical Ward (Male)`);
    await expect(message).toBeVisible();
    bed = /is in (.+)\.$/.exec(((await message.textContent()) ?? '').trim())![1];

    await expect(row).toContainText('Admitted');
    await expect(nurse.getByRole('dialog')).toHaveCount(0);
    await screenshot(nurse, 'PT-E2E-02-step6-nurse-bed-assigned');
  });

  await test.step('7. Patient sees the same bed and asks a care question (API)', async () => {
    const stay = await (await request.get(`${API_URL}/me/admission`, { headers: patientLogin })).json();
    expect(stay.status).toBe('admitted');
    expect(`${stay.ward_name} · ${stay.bed_number}`).toBe(bed);

    const question = await request.post(`${API_URL}/me/care-queries`, {
      headers: patientLogin,
      data: { reported_text: QUESTION },
    });
    expect(question.status()).toBe(202); // accepted, the agent works on it in the background
    const accepted = await question.json();
    workflowId = accepted.workflow_id;
    recommendationId = accepted.recommendation_id;
  });

  await test.step('7b. The agent finishes and writes a draft reply (API, as the nurse)', async () => {
    const nurseLogin = await request.post(`${API_URL}/auth/login`, { data: NURSE });
    const asNurse = { Authorization: `Bearer ${(await nurseLogin.json()).access_token}` };
    const agentRun = async () =>
      (await request.get(`${API_URL}/care-workflows/${workflowId}`, { headers: asNurse })).json();

    // Wait until the agent has stopped working. A Gemini call can take a while.
    await expect.poll(async () => (await agentRun()).status, { timeout: 200_000 }).not.toBe('running');
    const run = await agentRun();

    // Who wrote the draft: "model" (Gemini), "model_unavailable" (no answer, backup reply used)
    // or "model_rejected" (the safety check blocked Gemini's draft, backup reply used).
    test.info().annotations.push({ type: 'Draft source', description: run.draft_source });
    console.log(`Draft source this run: ${run.draft_source}`);
    if (run.draft_source === 'model_rejected') {
      test.info().annotations.push({ type: 'Safety check blocked the Gemini draft', description: run.draft_note });
    }

    expect(run.status).toBe('pending_review');
    expect(run.outcome).toBe('drafted');
    expect(['model', 'model_unavailable', 'model_rejected']).toContain(run.draft_source);
    expect(run.validation.passed).toBe(true); // the saved draft passed the safety check

    const recommendation = await request.get(`${API_URL}/care-recommendations/${recommendationId}`, { headers: asNurse });
    agentDraft = (await recommendation.json()).agent_message;
    expect(agentDraft.length).toBeGreaterThan(0);
  });

  await test.step('8. Negative: the patient cannot read the draft before a nurse approves it (API)', async () => {
    const mine = await (await request.get(`${API_URL}/me/care-recommendations`, { headers: patientLogin })).json();
    const item = mine.items.find((row: { id: string }) => row.id === recommendationId);
    expect(item.status).toBe('pending_review');
    expect(item.doctor_message).toBeNull();
    expect(item.reviewed_by_name).toBeNull();
    expect(Object.values(item)).not.toContain(agentDraft);
  });

  await test.step('9. Negative: the patient cannot open the staff review queue (API)', async () => {
    const queue = await request.get(`${API_URL}/care-recommendations`, { headers: patientLogin });
    expect(queue.status()).toBe(403);
  });

  await test.step('10. Ward nurse edits the draft and approves it (web)', async () => {
    await nurse.goto(`${WEB_URL}/care-recommendations`);
    await nurse.getByRole('row').filter({ hasText: patientCode }).getByRole('button', { name: 'Open' }).click();
    await expect(nurse.getByText(QUESTION)).toBeVisible();

    // The box starts with the agent's draft. The nurse adds one sentence and approves.
    const box = nurse.getByLabel('Message the patient will see');
    await expect(box).toHaveValue(agentDraft);
    approvedText = `${agentDraft} ${NURSE_NOTE}`;
    await box.fill(approvedText);
    await screenshot(nurse, 'PT-E2E-03-step10-nurse-edits-draft');
    await nurse.getByRole('button', { name: 'Approve', exact: true }).click();
    await expect(nurse.getByText('Approved. The patient can now see this.')).toBeVisible();

    // Open it again from the Approved tab and check what was sent.
    await nurse.getByRole('button', { name: 'Approved', exact: true }).click();
    await nurse.getByRole('row').filter({ hasText: patientCode }).getByRole('button', { name: 'Open' }).click();
    await expect(nurse.getByText('Message sent to the patient')).toBeVisible();
    await expect(nurse.getByText(NURSE_NOTE)).toBeVisible();
    await screenshot(nurse, 'PT-E2E-04-step10-approved');
  });

  await test.step("11. Patient reads the nurse's edited reply, not the original draft (API)", async () => {
    const mine = await (await request.get(`${API_URL}/me/care-recommendations`, { headers: patientLogin })).json();
    const item = mine.items.find((row: { id: string }) => row.id === recommendationId);
    expect(item.status).toBe('approved');
    expect(item.doctor_message).toBe(approvedText);
    expect(item.doctor_message).not.toBe(agentDraft);
    expect(item.reviewed_by_role).toBe('ward_nurse');
    expect(item.reviewed_by_name).toBeTruthy();
  });
});

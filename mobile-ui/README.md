# CareLanka — Flutter Mobile App (`mobile-ui`)

One Flutter app. Four members. Each member owns one folder under `lib/features/`.

This file explains the structure and the rules. **Read it before you write code —
and if you are an AI assistant, read it before you create a single file.**

---

## 1. Why this structure

We use a **feature-based** structure: code is grouped by *what part of the
business it belongs to*, not by *what type of file it is*.

This is what Lecture 02 teaches for React ("group by DOMAIN, not by type…
everything for a feature lives in one folder… features stay independent"), and
it calls the alternative — one giant `screens/` folder and one giant `models/`
folder — an anti-pattern. The same reasoning applies here.

For our group specifically it matters for two reasons:

- **Fewer merge conflicts.** Four people working in four different folders
  almost never touch the same file, so pull requests merge cleanly.
- **Easier for AI tools.** All four of us are using Claude Code. When each
  member's work lives in one folder, the AI has a small, clear area to read and
  change, instead of hunting through files that belong to three other people.

---

## 2. Folder layout

```text
mobile-ui/
├─ pubspec.yaml            # shared dependencies — coordinate before editing
├─ lib/
│  ├─ main.dart            # entry point
│  ├─ app.dart             # root widget (theme + router)
│  │
│  ├─ core/                # SHARED by everyone — see rules below
│  │  ├─ config/           # API base URL, environment settings
│  │  ├─ network/          # one HTTP client, auth header, error handling
│  │  ├─ auth/             # login, JWT storage, current role
│  │  ├─ routing/          # the app's route table
│  │  ├─ theme/            # colours, text styles
│  │  ├─ widgets/          # reusable UI: buttons, loading spinner, error box
│  │  └─ utils/            # date formatting, validators
│  │
│  └─ features/            # ONE FOLDER PER MEMBER
│     ├─ emergency/        # Member 1 — ambulance / emergency service
│     ├─ staff/            # Member 2 — staff management
│     ├─ equipment/        # Member 3 — health equipment
│     └─ patient/          # Member 4 — patient management
│
└─ test/                   # mirrors lib/ — tests live beside their feature
   ├─ core/
   └─ features/{emergency,staff,equipment,patient}/
```

Every feature folder has the same five sub-folders:

| Folder | What goes in it |
| :--- | :--- |
| `screens/` | Full pages — one per route |
| `widgets/` | Smaller pieces used by those screens |
| `models/` | **UI-only shapes, and usually empty.** See the note below |
| `services/` | This feature's wrapper over the generated API client |
| `state/` | State management (providers / controllers) |

**The layering rule:** screens never call `http` directly. A screen asks its
`state/`, which asks its `services/`, which uses `core/network/`. This is the
same separation the React lecture describes (UI → hooks → services → server),
and it is what makes the code testable.

> **`models/` and `services/` no longer mean what this table originally said**, and the
> patient feature is the worked example of the correction.
>
> This table used to read *"`models/`: Dart classes matching the API's JSON"* and
> *"`services/`: all HTTP calls for this feature"* — which is hand-writing a client, the one
> thing `CLAUDE.md` forbids outright. **Domain shapes come from the generated
> `lib/services/api_client/`**, which is committed (323 files on `main`), and a feature's
> `services/` wraps that client instead of calling `http` or `dio`. `core/network/` owns the
> JWT header and error handling once, for everyone.
>
> `lib/features/patient/` is built this way today: `models/` is empty, and
> `services/patient_service.dart` imports `care_lanka_api.dart` and the generated model
> classes and calls them. A `models/` folder only earns a file when the UI needs a shape the
> API does not have — a tab enum, a form's draft state — never a copy of a response.
>
> `CLAUDE.md` records this as the group's working resolution. One question is still genuinely
> open and is **not** answered by the patient feature: whether `lib/services/api_client/`
> stays committed or is regenerated in CI.

---

## 3. Rules — for people and for AI assistants

1. **Work only inside your own feature folder.** `lib/features/patient/` is
   Member 4's. Do not add, edit or delete files in another member's folder.
2. **`core/` is shared.** If you need something added to `core/` (a new shared
   widget, a change to the HTTP client, a new route), ask the group first. Do
   not quietly change how the API client or the router behaves — everyone
   depends on it.
3. **`pubspec.yaml` is shared.** Adding a package is fine; tell the group so two
   people don't edit it at the same time and cause a conflict.
4. **Never call another member's service class directly.** If your feature needs
   data that another member owns, raise it in
   [`specs/integration_of_functions.md`](../specs/integration_of_functions.md)
   and agree an API endpoint. Do not invent their endpoint yourself.
5. **The API is the contract.** What each endpoint does is defined in the
   `specs/*.yaml` files, not guessed from the Dart code.
6. **Don't touch other members' routes.** `core/routing/` holds everyone's
   routes; add yours, leave theirs alone.

> These mirror the rules in `specs/integration_of_functions.md`. If the two ever
> disagree, that file wins.

---

## 4. What the rubric wants from this app

From the assignment (§8 and the Flutter marking row), the finished app must show:

- [ ] Reusable widgets
- [ ] Routing
- [ ] State management
- [ ] Secure API integration (JWT stored securely, not in plain text)
- [ ] Input validation
- [ ] Responsive screens
- [ ] Loading **and** error states on every screen that calls the API
- [ ] At least one meaningful device feature
- [ ] At least 3 roles with clearly different screens

The structure above gives each of these a place to live. Ticking them is each
member's own job inside their feature folder.

---

## 5. Getting started

> **The one-time setup is done.** `android/` and `ios/` are committed. This section used to
> say they did not exist and that one person had to run `flutter create .` first — that
> happened, and the folders are on `main`. **Do not run `flutter create .` again.**

```bash
cd mobile-ui
flutter pub get
flutter run
```

If `flutter run` cannot find a device, `flutter devices` lists what it can see; Chrome counts
as one, and the patient screens are built and tested against it.

---

## 6. Still to be decided by the group

These are shared decisions, not one member's call:

- **Who owns `core/`?** Suggestion: one person for the whole group, so the HTTP client, auth
  and router stay consistent. Others request changes rather than making them.
- **State management choice.** `provider` is listed in `pubspec.yaml` as a
  starting point because it is the simplest option that satisfies the rubric.
  If the group prefers Riverpod or Bloc, change it now, before anyone builds
  screens on top of it.
- **API base URL handling.** Needs to differ between a local machine, an
  emulator and the deployed API — belongs in `core/config/`.

# SE3090: Software Engineering Frameworks

**Year 3 Semester 1 – 2026**  
**Assignment 2**  
**BSc (Hons) in Information Technology – Software Engineering**

---

## Assignment Information

**Assignment Title:** Software Testing and Quality Evaluation of the SE3090 Integrated System

**Learning Outcomes Covered**

**LO2:** Apply suitable frameworks and tools to build web, mobile, and full-stack software applications efficiently and effectively.

**LO3:** Use best practices for integrating frameworks, managing collaborative development, applying CI/CD, ensuring code quality, and deploying software solutions.

**Assignment Mode:** Group assignment with individual viva

**Maximum Marks:** 100 Marks

**Contribution to the Final Grade:** 15%

**Date Published:** 19th September 2026

**Deadline for Submissions:** 5th October 2026

**Mode of Submission:** Submit through the official Learning Management System (CourseWeb).

---

# Assignment Description

This assignment is directly connected to the SE3090 Main Assignment. You must test the same integrated system developed by your group. You are not required to build a separate application for this assessment.

The purpose is to show that your system has been tested in a planned and professional way. You must select suitable testing areas, use appropriate testing tools or frameworks, execute the tests, record the results, identify defects, and provide clear evidence of your testing work.

---

# 1. Relationship to the SE3090 Main Assignment

- Use the same ASP.NET Core Web API, PostgreSQL database, React web application, Flutter mobile application and Agentic AI subsystem developed for the SE3090 Main Assignment.
- Test the actual features and workflows implemented by your group.
- At least one test must cover a complete integrated workflow across the relevant components of the system.
- The testing evidence produced here can also support the testing-related documentation required for the SE3090 Main Assignment.

---

# 2. Testing Scope

Your group must perform suitable testing from the areas below.

For each selected technical testing area, you must use an appropriate testing tool or framework.

**Manual observation alone is not sufficient.**

## 2.1 Backend / API Testing

### What to Test
- Unit testing
- Service/business-logic testing
- Validation testing
- Controller testing
- Authentication and authorization testing
- API integration testing

### Suggested Tools / Frameworks
- xUnit
- NUnit
- MSTest
- Moq
- WebApplicationFactory
- Postman/Newman

---

## 2.2 Database Testing

### What to Test
- Database integration testing
- Constraint testing
- Relationship and data-integrity testing
- Migration testing
- Transaction testing

### Suggested Tools / Frameworks
- xUnit + PostgreSQL
- Testcontainers for .NET
- Entity Framework Core

---

## 2.3 React Web Application Testing

### What to Test
- Component testing
- Form-validation testing
- Protected-route testing
- API-integration testing
- UI-state and error-state testing

### Suggested Tools / Frameworks
- Vitest/Jest
- React Testing Library
- MSW
- Playwright

---

## 2.4 Flutter Mobile Application Testing

### What to Test
- Unit testing
- Widget testing
- Form-validation testing
- Navigation testing
- API-integration testing

### Suggested Tools / Frameworks
- flutter_test
- integration_test
- mocktail/Mockito

---

## 2.5 Integration / End-to-End Testing

### What to Test
- API integration testing
- Cross-component integration testing
- Complete business-workflow testing
- Cross-platform workflow testing

### Suggested Tools / Frameworks
- Playwright
- Postman/Newman
- Flutter integration_test
- Another justified E2E tool

---

## 2.6 Non-Functional Testing

### What to Test
- Performance testing
- Load testing
- Stress testing
- Security testing
- Usability testing
- Accessibility testing
- Compatibility testing
- Reliability testing
- Recovery testing

### Suggested Tools / Frameworks
- k6
- Apache JMeter
- OWASP ZAP
- Lighthouse
- axe DevTools
- Playwright
- Other appropriate monitoring/testing tools

### Required Non-Functional Testing

**Performance and security testing are required.**

Select additional non-functional testing types where relevant to your system and justify your selection.

---

## 2.7 Agentic AI Testing & Evaluation

### What to Test
- Task-completion testing
- Agent-selection testing
- Tool-selection testing
- Structured-output validation
- Business-rule compliance testing
- Prompt-injection testing
- Approval-enforcement testing
- Failure-recovery testing
- Safe-failure testing

### Suggested Tools / Frameworks
- xUnit/pytest
- promptfoo
- DeepEval
- Schema validation
- Deterministic test cases

---

# 3. What You Need to Do

1. Identify the important features, workflows and quality risks in your SE3090 system.
2. Prepare a test plan showing what will be tested, the testing type, expected result, tool/framework and responsible member.
3. Design meaningful test cases, including normal, invalid, boundary/edge and failure cases where relevant.
4. Use suitable testing tools/frameworks and execute the tests.
5. Record actual results and clearly mark each test as Passed or Failed.
6. Record defects found, correct important defects and perform retesting.
7. Collect evidence such as automated test output, screenshots, logs, coverage, performance results, security scan results or AI evaluation results.
8. Prepare the required testing documents and submit them with the supporting evidence.
9. Prepare to explain and demonstrate your own testing contribution during the viva.

---

# 4. Testing Documents to Prepare

## 4.1 Test Plan

The Test Plan must include:

- Scope
- Objectives
- Testing areas
- Tools/frameworks
- Test environment
- Responsibilities
- Schedule

---

## 4.2 Test Case Document

The Test Case Document must include:

- Test case ID
- Feature
- Preconditions
- Steps/input
- Expected result
- Actual result
- Pass/Fail status

---

## 4.3 Defect / Bug Report

The Defect / Bug Report must include:

- Defect ID
- Description
- Severity/priority
- Steps to reproduce
- Evidence
- Status
- Retest result

---

## 4.4 Test Execution Summary

The Test Execution Summary must include:

- Tests executed
- Tests passed
- Tests failed
- Defects identified/fixed
- A short conclusion

---

## 4.5 Tool-Generated Evidence

Include relevant:

- Automated test reports
- Coverage
- Performance results
- Security scans
- Logs
- AI evaluation outputs

---

# 5. Deliverables

You must submit the following:

- **Software Testing Report (PDF)** containing:
  - Test plan
  - Testing scope
  - Test execution summary
  - Defect summary
  - Conclusion

- **Completed Test Case Document** with:
  - Expected result
  - Actual result
  - Pass/Fail status

- **Defect / Bug Report** with retesting evidence.

- **Testing tool/framework evidence**, including relevant:
  - Screenshots
  - Generated reports
  - Logs
  - Exported results

- **Automated test source code/scripts** used for the applicable testing areas.

- **GitHub repository link** and contribution/commit evidence related to testing.

- **Any additional configuration or files** required to reproduce or rerun the tests.

> **Important:** Evidence must come from your own SE3090 system. A submission containing only theoretical descriptions of testing will not receive full marks.

---

# 6. Viva

A viva will be conducted as part of this assignment.

Each student must be able to explain:

- The tests they contributed to
- The selected tool/framework
- How the test was executed
- What the result means
- Defects found
- How the system was improved

Students may also be asked to:

- Run a test
- Modify a test
- Explain a test during the viva

**Individual viva performance may affect the individual mark.**

---

# 7. Usage of AI

AI tools may be used to support:

- Learning
- Brainstorming
- Test-case ideas
- Debugging
- Test-script generation
- Documentation
- Code review

Students are responsible for checking, adapting and understanding all AI-assisted work.

AI-generated test cases or scripts must be verified against the actual system before submission.

Students must not submit testing work they cannot explain or reproduce during the viva.

AI assistance must be declared according to the module requirements and the CLEAR framework.

---

# 8. Marking Scheme

**Total:** 100 marks

**Group Contribution:** 40 marks

**Individual Contribution:** 60 marks

**Contribution to the Final Module Grade:** 15%

> **Important:** This assessment is demonstration-based. Submitted documents and screenshots alone are not sufficient. Each student must personally demonstrate and explain at least one meaningful tool/framework-based testing contribution using the group’s SE3090 system.

---

# Detailed Marking Rubric

## GROUP: Testing Strategy & Coverage — 10 Marks

### Excellent
Clearly explains a well-planned testing strategy. Testing scope is relevant and covers the important system components, workflows, and risks with suitable tools/frameworks.

### Good
Good strategy and coverage of most important areas with only minor gaps.

### Satisfactory
Basic strategy covers the main areas, but some important components, risks or test types are missing.

### Poor
Limited testing strategy; coverage is narrow or poorly justified.

### Very Poor
No meaningful testing strategy or coverage is demonstrated.

---

## GROUP: Integrated & Non-Functional Testing Demonstration — 15 Marks

### Excellent
Successfully demonstrates meaningful integration/E2E testing and relevant non-functional testing using suitable tools. Results are clearly interpreted and connected to the actual SE3090 system.

### Good
Demonstrates integration and non-functional testing effectively with minor gaps in coverage, execution or explanation.

### Satisfactory
Basic demonstrations are completed, but coverage, tool use or interpretation is limited.

### Poor
Demonstration is incomplete, mostly manual, or provides weak evidence of integrated/non-functional testing.

### Very Poor
No meaningful integrated or non-functional testing demonstration.

---

## GROUP: Overall Test Results, Defects & Documentation — 15 Marks

### Excellent
Test results are complete and traceable. Defects are clearly recorded, important fixes are shown with retesting, and required testing documents/tool evidence are complete and consistent.

### Good
Good results, defect handling and documentation with minor omissions.

### Satisfactory
Main documents and results are present, but defect/retest evidence or consistency is basic.

### Poor
Documents/results are incomplete or poorly supported by actual testing evidence.

### Very Poor
Major testing documents, results or defect evidence are missing.

---

## INDIVIDUAL: Testing Tool / Framework Demonstration — 15 Marks

### Excellent
Personally and confidently demonstrates appropriate testing tool(s)/framework(s), explains why they were selected, configuration/setup, and how they are used on the actual system.

### Good
Demonstrates suitable tool/framework use with good understanding and only minor gaps.

### Satisfactory
Can run and explain basic tool/framework usage but shows limited understanding of configuration or purpose.

### Poor
Limited demonstration; relies heavily on others or cannot clearly explain how the tool/framework is used.

### Very Poor
Cannot demonstrate meaningful personal use of a testing tool/framework.

---

## INDIVIDUAL: Test Implementation & Execution — 15 Marks

### Excellent
Shows meaningful personally implemented tests and executes them successfully. Test design includes appropriate normal, invalid, boundary/edge and/or failure cases and meaningful assertions/checks.

### Good
Good implementation and execution with minor gaps in test variety, assertions or coverage.

### Satisfactory
Basic tests are implemented and run, but scenarios or assertions are limited.

### Poor
Few/trivial tests, weak implementation, or difficulty executing own tests.

### Very Poor
Cannot show or execute meaningful personally implemented tests.

---

## INDIVIDUAL: Results, Defects & Retesting — 10 Marks

### Excellent
Clearly interprets own test results, identifies meaningful defects/issues, explains their cause/fix where applicable, and demonstrates retesting or verification.

### Good
Good understanding of results and defect handling with minor gaps.

### Satisfactory
Can explain basic results and some defect/retest evidence, but analysis is limited.

### Poor
Results are shown with little interpretation or weak defect/retest evidence.

### Very Poor
Cannot explain test results or provide meaningful defect/retesting evidence.

---

## INDIVIDUAL: Technical Contribution — 5 Marks

### Excellent
Clear individual ownership is visible through test code/scripts, Git history and related fixes/evidence; contribution is consistent and traceable.

### Good
Good identifiable contribution with minor gaps in traceability.

### Satisfactory
Some identifiable contribution exists, but ownership/evidence is limited.

### Poor
Very limited or unclear individual contribution.

### Very Poor
No identifiable individual testing contribution.

---

## INDIVIDUAL: Viva & Technical Understanding — 15 Marks

### Excellent
Demonstrates strong understanding of own testing approach, code/scripts, tools, results and related system behaviour; confidently answers questions and can run, explain, modify or troubleshoot a test when requested.

### Good
Good understanding and demonstration with minor difficulty on advanced questions or modifications.

### Satisfactory
Basic understanding, but has difficulty explaining some technical decisions, results or test changes.

### Poor
Weak understanding and significant difficulty explaining or modifying submitted testing work.

### Very Poor
Cannot explain, reproduce, modify or demonstrate submitted testing work.

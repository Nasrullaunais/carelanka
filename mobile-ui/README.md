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
| `models/` | Dart classes matching the API's JSON |
| `services/` | All HTTP calls for this feature |
| `state/` | State management (providers / controllers) |

**The layering rule:** screens never call `http` directly. A screen asks its
`state/`, which asks its `services/`, which uses `core/network/`. This is the
same separation the React lecture describes (UI → hooks → services → server),
and it is what makes the code testable.

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

> **⚠️ One-time setup — this must be done once, by one person, before anyone
> can run the app.**
>
> The Flutter SDK was not installed on the machine where this skeleton was
> created, so the platform folders (`android/` and `ios/`) **do not exist yet**.
> Until someone generates them, `flutter run` will not work for anybody.

**Whoever sets up first (only one person needs to do this):**

```bash
cd mobile-ui
flutter create .          # adds android/ and ios/ — does NOT touch lib/
flutter pub get
flutter run               # check it launches
```

Then commit the generated folders so nobody else has to repeat it:

```bash
git add android ios
git commit -m "Add Flutter platform folders (android/ios)"
```

`flutter create .` is safe to run here. Because `pubspec.yaml` already exists,
Flutter fills in only what is missing — it leaves `lib/`, `pubspec.yaml`, this
README and everyone's feature folders exactly as they are.

**Everyone else, after that person has pushed:**

```bash
git pull
cd mobile-ui
flutter pub get
flutter run
```

If you pull and `android/` still isn't there, ask in the group chat who is doing
the setup — don't run `flutter create .` a second time in parallel.

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

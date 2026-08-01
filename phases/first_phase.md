# First Phase — Race Time Tracker MVP

## 1. Objective

Build a Windows desktop application for timing runners at amateur competitions.

The MVP must:

- run as a WPF application on .NET 10;
- use one window;
- start a new competition or resume an unfinished one;
- show the elapsed competition time;
- record runner passages by start number;
- finish the active competition;
- persist competition metadata to JSON and passages to CSV;
- be publishable as a single-file executable.

The application is intended for rapid keyboard-driven operation. Reliability of recorded timestamps and immediate readiness for the next runner are more important than visual complexity.

## 2. Scope

### In scope

- A single active competition.
- Local file persistence.
- Competition start and finish actions.
- Live elapsed-time display.
- Runner start-number entry using the `Ok` button or Enter key.
- Multiple passage records for the same runner.
- Recovery of an unfinished competition after an application restart.
- Input validation and user-visible error handling.
- Single-file Windows executable publishing.
- Unit tests for domain and persistence behavior.

### Out of scope

- Runner registration or participant names.
- Editing or deleting recorded passages.
- Multiple simultaneous competitions.
- Rankings, results, lap calculation, reports, or exports other than the required CSV.
- Network synchronization, cloud storage, or a database.
- User accounts, roles, and permissions.
- Importing existing competition data.
- Automatic backup or file rotation.
- Installer creation and code signing.

## 3. Recommended technical baseline

- **Framework:** .NET 10
- **UI:** WPF
- **Language:** C# with nullable reference types enabled
- **Architecture:** lightweight MVVM with dependency injection
- **JSON:** `System.Text.Json`
- **Tests:** xUnit
- **Target framework:** `net10.0-windows`
- **Runtime identifier:** `win-x64` for the first distributable build

Use framework features and a small solution structure rather than introducing a full application framework for this MVP.

Suggested projects:

```text
RaceTimeTracker.sln
src/
  RaceTimeTracker.App/
    App.xaml
    MainWindow.xaml
    ViewModels/
    Domain/
    Application/
    Infrastructure/
tests/
  RaceTimeTracker.Tests/
phases/
  first_phase.md
```

## 4. User experience

### Main window

The window contains:

1. A competition action button displaying `Start` or `Finish`.
2. A read-only elapsed-time display.
3. A start-number input.
4. An `Ok` button.
5. A compact status/error message area.

### Initial state

On launch:

- the action button displays `Start`;
- elapsed time displays `00:00:00`;
- passage entry is disabled until a competition is active;
- the start-number input receives keyboard focus.

If an unfinished competition exists, clicking `Start` resumes it instead of creating another competition.

### Active state

After a competition is started or resumed:

- the action button displays `Finish`;
- the elapsed-time display updates from the persisted competition start time;
- passage entry is enabled;
- focus is placed in the start-number input.

### Finished state

After `Finish`:

- the timer stops;
- the finish time is persisted;
- the action button displays `Start`;
- elapsed time retains the final value until another competition starts;
- passage entry is disabled;
- focus remains associated with the start-number input for the next operating cycle.

## 5. Functional requirements

### FR-01 — Start a new competition

When the user clicks `Start` and no unfinished competition exists:

1. Capture one local timestamp using `DateTime.Now`.
2. Generate a unique name in the format `competition_NNNN`, for example `competition_6578`.
3. Add a competition to `competitions.json` with:
   - `startTime` set to the captured timestamp;
   - `finishTime` set to `null`.
4. Persist the JSON successfully.
5. Create the corresponding CSV file and its header if it does not exist.
6. Make the new competition active.
7. Start displaying elapsed time.
8. Change the button text to `Finish`.

The active state must only be entered after the metadata is successfully persisted.

### FR-02 — Resume an unfinished competition

When the user clicks `Start` and `competitions.json` contains an entry whose `finishTime` is `null`:

- use its persisted name and `startTime`;
- do not replace or recalculate the start time;
- do not create another JSON entry;
- ensure its CSV file and header exist;
- calculate elapsed time from the persisted start time;
- change the button text to `Finish`.

The repository should enforce the invariant that at most one unfinished competition exists.

For defensive recovery from manually corrupted data containing multiple unfinished competitions, select the entry with the latest `startTime`, leave the file unchanged, and show an error requiring manual correction before accepting passages. This avoids silently recording against an arbitrary competition.

### FR-03 — Display elapsed time

- Elapsed time is `DateTime.Now - activeCompetition.startTime`.
- Update the display at least once per second.
- Display a duration, not a time of day.
- Recommended format:
  - `HH:mm:ss` for durations below 24 hours;
  - total hours with no 24-hour wrap for longer competitions.
- The UI timer is only responsible for refreshing the display. Recorded passage times must be calculated from a newly captured timestamp, not from the displayed timer value.

### FR-04 — Record a runner passage

When a competition is active and the user clicks `Ok` or presses Enter in the input:

1. Validate the entered start number.
2. Capture the passage timestamp once.
3. Calculate `time_elapsed = passageTimestamp - competition.startTime`.
4. Append one record to `<competition_name>.csv`.
5. Flush and close the file write.
6. Clear the input only after a successful append.
7. Restore focus to the input and select its contents as appropriate.
8. Show a short success indication without interrupting keyboard entry.

CSV format:

```csv
start_number,time_elapsed
42,00:05:37.284
42,00:12:19.041
007,00:13:02.715
```

Decisions:

- Treat `start_number` as text so leading zeroes are preserved.
- Accept only non-empty trimmed input containing digits.
- Store `time_elapsed` using invariant culture and the format `hh:mm:ss.fff`; if a competition can exceed 24 hours, write total hours rather than wrapping to zero.
- Write fields using standard CSV escaping even though the initial validation allows digits only.
- Repeated start numbers are valid and append additional rows.

### FR-05 — Finish a competition

When the user clicks `Finish`:

1. Capture one local timestamp using `DateTime.Now`.
2. Set the active competition's `finishTime`.
3. Persist `competitions.json`.
4. Stop refreshing the timer.
5. Disable passage entry.
6. Change the action button text to `Start`.
7. Clear the in-memory active competition only after persistence succeeds.

If saving fails, the application must remain in the active state and clearly report the error. It must not present the competition as finished.

### FR-06 — Keyboard behavior and persistent focus

- Enter in the start-number input invokes the same command as `Ok`.
- After a successful or failed entry attempt, focus returns to the input.
- Clicking `Start`, `Finish`, or `Ok` must not permanently take focus from the input.
- When the window is reactivated, focus returns to the input.
- Global focus must not prevent normal window controls, accessibility behavior, or application shutdown.

Implement focus as a small view concern (for example, a focus behavior or code-behind event), while keeping competition logic in the view model.

## 6. Persistence contract

### Storage location

Place data beside the executable when that directory is writable, matching the requirement that files are created “in the folder.”

At startup, test or handle write access gracefully. If the executable directory is not writable, prevent timing operations and show the full expected path with a useful error. The MVP must not silently switch storage locations because operators need predictable access to competition files.

### `competitions.json`

The root object is keyed by competition name:

```json
{
  "competition_6578": {
    "startTime": "2026-07-30T16:42:10.1234567+03:00",
    "finishTime": null
  },
  "competition_4021": {
    "startTime": "2026-07-29T09:00:00.0000000+03:00",
    "finishTime": "2026-07-29T11:13:45.0000000+03:00"
  }
}
```

Persistence rules:

- Use camel-case property names.
- Serialize timestamps using the round-trip ISO 8601 (`O`) representation.
- Serialize an unfinished competition with `"finishTime": null`.
- Preserve all existing valid competition entries when updating one entry.
- Write JSON through a temporary file and atomically replace the target where supported, reducing the chance of truncation after a crash.
- Serialize file operations within the process so rapid repeated UI actions cannot overlap writes.
- Treat an empty, malformed, or schema-invalid JSON file as an error; do not overwrite it automatically.

Although the requirement says “empty finishTime,” JSON `null` is the explicit representation selected for the MVP.

### Competition names

- Format: `competition_NNNN`.
- Generate `NNNN` in the range `1000`–`9999`.
- Use a random-number generator suitable for collision-resistant local identifiers.
- Regenerate if either the JSON key or target CSV filename already exists.
- If the name space is exhausted, fail with a user-visible error rather than overwrite data.

### `<competition_name>.csv`

- Use UTF-8 without a byte-order mark.
- Use comma as the delimiter regardless of machine culture.
- Use `\r\n` line endings for Windows compatibility.
- Create the header exactly once.
- Append one complete line per accepted passage.
- Never rewrite existing passage rows during normal operation.

## 7. Architecture and responsibilities

### Domain

`Competition`

- `Name`
- `StartTime`
- `FinishTime`
- derived `IsActive`

Domain rules:

- start time is required;
- finish time is either absent or greater than/equal to start time;
- only an active competition can accept passages;
- elapsed time cannot be negative.

### Application services

`CompetitionService`

- starts a new competition;
- resumes an existing competition;
- finishes the active competition;
- exposes current state.

`PassageService`

- validates a start number;
- calculates elapsed time;
- appends a passage.

`IClock`

- exposes the current local time;
- allows deterministic unit tests.

`ICompetitionNameGenerator`

- generates candidate names independently of persistence.

### Infrastructure

`ICompetitionRepository`

- loads all competition metadata;
- finds unfinished competitions;
- adds a competition;
- updates the finish time.

`IPassageWriter`

- ensures the CSV exists with a header;
- appends a passage record.

Concrete implementations use `System.Text.Json` and file I/O.

### Presentation

`MainWindowViewModel`

- exposes start/finish and record-passage commands;
- exposes button text, input state, elapsed time, and status;
- owns the UI refresh timer lifecycle;
- prevents command re-entry while file operations are in progress.

`MainWindow`

- binds controls to the view model;
- handles view-only focus restoration;
- defines Enter as the passage-command input gesture.

File I/O should not execute concurrently. Small writes may be asynchronous, but correctness and command serialization are mandatory.

## 8. Error handling

The app must remain open and show an actionable message for:

- unavailable or non-writable data directory;
- malformed `competitions.json`;
- invalid timestamp or missing required JSON property;
- multiple unfinished competitions;
- failure to create or update JSON;
- failure to create or append CSV;
- invalid or empty start number;
- negative elapsed time caused by system-clock changes.

Operational rules:

- Do not clear runner input when a CSV append fails.
- Do not change `Start` to `Finish` when starting/resuming persistence fails.
- Do not change `Finish` to `Start` when finish persistence fails.
- Disable affected commands while an operation is executing to prevent double submission.
- Do not display raw stack traces to the operator; retain exception details for debugger or diagnostic logging.

## 9. Single-file publishing

Add a publish profile or documented command equivalent to:

```powershell
dotnet publish src/RaceTimeTracker.App/RaceTimeTracker.App.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true
```

Project properties should include:

```xml
<TargetFramework>net10.0-windows</TargetFramework>
<UseWPF>true</UseWPF>
<PublishSingleFile>true</PublishSingleFile>
<SelfContained>true</SelfContained>
<RuntimeIdentifier>win-x64</RuntimeIdentifier>
```

Trimming should be disabled for the MVP unless a verified WPF-compatible configuration is introduced. “Single file” means one distributed application executable; the application will still create JSON and CSV data files at runtime.

## 10. Work breakdown

### Task 1 — Bootstrap solution

- Create application and test projects.
- Configure .NET 10, WPF, nullable reference types, and warnings.
- Add dependency-injection composition in `App`.
- Add the single-file publish profile.

Deliverable: buildable solution and application shell.

### Task 2 — Define domain and contracts

- Add the competition model and invariants.
- Add persistence, clock, and name-generator abstractions.
- Define typed operation results or application exceptions.

Deliverable: independently testable core contracts.

### Task 3 — Implement JSON repository

- Load and validate `competitions.json`.
- Create the file when absent.
- Add and finish competitions without losing historical entries.
- Implement safe replacement and in-process write serialization.
- Detect zero, one, or multiple active competitions.

Deliverable: tested competition metadata persistence.

### Task 4 — Implement CSV passage writer

- Create competition CSV with its header.
- Append escaped UTF-8 records.
- Format elapsed durations invariantly.
- Ensure failed writes do not appear successful.

Deliverable: tested append-only passage storage.

### Task 5 — Implement application services

- Start or resume a competition.
- Finish a competition.
- Validate and record passages.
- Use injected clock and name generator.

Deliverable: UI-independent MVP workflow.

### Task 6 — Implement main window

- Create start/finish button, timer display, start-number input, `Ok` button, and status area.
- Bind commands and enabled states.
- Add Enter-key submission.
- Implement resilient focus restoration.
- Guard against command re-entry.

Deliverable: complete operator workflow.

### Task 7 — Verification and packaging

- Run unit tests.
- Execute manual acceptance scenarios.
- Publish for `win-x64`.
- Run the published executable from a writable folder on a clean Windows machine or VM.
- Verify that JSON and CSV files are created beside it.

Deliverable: verified single-file MVP executable.

## 11. Test strategy

### Unit tests

- Starting with no JSON file creates one active competition.
- Generated competition names match the required pattern.
- A collision causes regeneration rather than overwrite.
- Starting with one unfinished competition resumes it.
- Finished competitions are not resumed.
- Finishing sets only the active competition's finish time.
- JSON round-trips timestamps and null finish time.
- Malformed JSON is rejected and preserved.
- Multiple active competitions are rejected for recording.
- CSV header is created once.
- Each passage appends exactly one row.
- Duplicate start numbers are accepted.
- Leading zeroes in start numbers are preserved.
- Empty and non-digit start numbers are rejected.
- Elapsed time uses the passage timestamp and persisted start time.
- Durations over 24 hours do not wrap.
- Negative elapsed time is rejected.

### UI or view-model tests

- Button text and command availability reflect application state.
- Enter invokes the same passage command as `Ok`.
- Successful recording clears the input.
- Failed recording retains the input.
- Repeated invocation while a write is active is ignored or disabled.
- Timer refresh does not determine the stored passage time.

### Manual acceptance tests

1. Launch from an empty writable directory, start a competition, record two different runners, record a second lap for one runner, and finish.
2. Confirm JSON contains one finished competition with valid ISO 8601 timestamps.
3. Confirm CSV has one header and three records in input order.
4. Start a competition, close the app without finishing, relaunch, click `Start`, and confirm the same competition resumes.
5. Press Enter repeatedly with valid numbers and confirm focus always returns to the input.
6. Make the storage folder read-only and confirm the app reports the failure without false state transitions.
7. Corrupt the JSON and confirm the app does not overwrite it.
8. Run the published single executable on the target Windows architecture.

## 12. Definition of done

The first phase is complete when:

- all functional requirements in this document are implemented;
- automated tests pass;
- the manual acceptance scenarios pass;
- the app creates and updates the specified JSON structure;
- runner passages are appended to the correctly named CSV;
- duplicate runner numbers create additional rows;
- unfinished competition recovery works after restart;
- keyboard entry and focus behavior are reliable;
- errors do not produce false UI state or silent data loss;
- a Release `win-x64` self-contained single-file executable is produced and smoke-tested;
- basic build, run, publish, storage-location, and file-format instructions are documented.

## 13. Assumptions requiring product confirmation before later phases

These decisions are safe for the MVP but should be revisited before expanding the product:

- start numbers contain digits only;
- local wall-clock time is authoritative;
- one unfinished competition is allowed globally;
- data is stored beside the executable;
- operators manually choose when to resume by clicking `Start`;
- milliseconds are sufficient for recorded precision;
- `win-x64` is the only first-phase distribution target;
- no record correction workflow is required.

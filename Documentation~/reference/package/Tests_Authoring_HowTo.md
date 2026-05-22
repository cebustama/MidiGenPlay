# Reference — Tests Authoring How-To

> Non-authoritative operational guide for adding and running EditMode/PlayMode
> tests inside the MidiGenPlay package. Captured at Phase 7 closure, when the
> package's first test assembly (`MidiGenPlay.Tests.Editor`) was added.
>
> Authority: this document is **reference**, below SSoTs and below
> `SSoT_CONTRACTS.md` in the authority order defined by `SSoT_INDEX.md`. If
> the authoritative `MidiGenPlay.Tests.Editor.asmdef` ever diverges from the
> shape below, the asmdef wins and this document is updated to match.

## 1. Where tests live

EditMode tests live in `Packages/MidiGenPlay/Tests/Editor/`. The asmdef
boundary at this folder defines the test assembly. PlayMode tests, if and
when added, would live in `Packages/MidiGenPlay/Tests/Runtime/` under a
separate asmdef.

The package's first test assembly is `MidiGenPlay.Tests.Editor`. It is
registered for Test Runner discovery via the `testables` field of the
package's `package.json`.

## 2. The asmdef contract

The canonical shape for an EditMode test asmdef inside this package:

```json
{
    "name": "MidiGenPlay.Tests.Editor",
    "rootNamespace": "MidiGenPlay.Tests.Editor",
    "references": [
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner",
        "MidiGenPlay.Runtime",
        "MidiGenPlay.Editor"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll"
    ],
    "autoReferenced": false,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

### Field rationale

- **`name`** — asmdef's assembly name; this is the string used by `testables`
  in `package.json` and by other asmdefs that reference this one.
- **`references`** — `UnityEngine.TestRunner` + `UnityEditor.TestRunner` are
  **required** for Test Runner to discover tests in this assembly when the
  asmdef lives in a package (the precompiled `nunit.framework.dll`
  reference alone is not sufficient in local-package contexts on Unity 2022.3).
  The target package asmdefs (`MidiGenPlay.Runtime`, `MidiGenPlay.Editor`) are
  added so the test code can call into the code under test.
- **`includePlatforms: ["Editor"]`** — EditMode only. PlayMode test asmdefs
  use `includePlatforms: []` + `excludePlatforms: []`.
- **`overrideReferences: true`** + **`precompiledReferences:
  ["nunit.framework.dll"]`** — explicitly bring in NUnit.
- **`autoReferenced: false`** — non-test asmdefs do not auto-reference the
  test asmdef.
- **`defineConstraints: []`** — **leave empty.** See §2.1 below.

### 2.1. The `UNITY_INCLUDE_TESTS` gotcha

The standard Unity test-asmdef template (auto-generated via
Create → Testing → Tests Assembly Folder) includes
`defineConstraints: ["UNITY_INCLUDE_TESTS"]`. The intent of that constraint
is that the asmdef compiles only when the Test Framework package is
installed — protecting non-test builds from compile failures if a consumer
disables Test Framework.

In this package's setup (local package, Unity 2022.3) that constraint
**silently blocks compilation**: the symbol does not propagate to the
asmdef's compilation context, the asmdef is not compiled, no DLL is
produced, no tests are discovered, and there is no error in the Console.
Test Runner simply shows "No tests to show."

Resolution: leave `defineConstraints` empty. The asmdef compiles
unconditionally. The trade-off (build failures if Test Framework is removed)
is acceptable for a local package in active development. If MidiGenPlay is
later published to a public registry, this decision should be revisited.

This gotcha cost four debugging hours during Phase 7. Documented here so
the next reader doesn't repeat it.

## 3. The `package.json` handshake

Test discovery inside a package requires `testables` at the root level of
`package.json`:

```json
{
    "name": "com.claudiobustamante.midigenplay",
    ...
    "testables": [
        "MidiGenPlay.Tests.Editor"
    ]
}
```

The value of each entry is the asmdef's `"name"` field (the assembly name) —
not the asmdef filename, not the package's technical name. Multiple
assemblies can be listed; one per line.

Without `testables`, Unity's Test Runner ignores tests inside the package
entirely, regardless of the asmdef shape. This is by design — it prevents
random consumed packages from polluting the consuming project's Test Runner.

## 4. Adding a new test file

To add a new EditMode test to `MidiGenPlay.Tests.Editor`:

1. Drop a `.cs` file into `Packages/MidiGenPlay/Tests/Editor/`.
2. Use namespace `MidiGenPlay.Tests.Editor` (matches the asmdef's
   `rootNamespace`).
3. Declare the test class as `public`.
4. Declare each test method as `public void` with the `[Test]` attribute
   from `NUnit.Framework`.
5. `[TestFixture]` on the class is **not required** — Unity Test Framework
   auto-discovers `[Test]`-decorated methods.

Minimum file shape:

```csharp
using NUnit.Framework;
using MidiGenPlay;
using MidiGenPlay.Authoring; // import whatever you're testing

namespace MidiGenPlay.Tests.Editor
{
    public class MyFeatureTests
    {
        [Test]
        public void MyFeature_DoesTheRightThing()
        {
            // arrange / act
            int result = 1 + 1;
            // assert
            Assert.AreEqual(2, result);
        }
    }
}
```

The `#if UNITY_EDITOR ... #endif` guard around the file is redundant
(the asmdef is editor-only via `includePlatforms`) but harmless.

## 5. Running tests

1. Window → General → Test Runner.
2. Switch to the **EditMode** tab.
3. The test assembly appears as
   `MidiGenPlay.Tests.Editor.dll → MidiGenPlay → Tests → Editor →
   <YourTestClass>`.
4. Click **Run All** to run every test, or right-click a class/method and
   choose **Run** to run a subset.

## 6. Adding a new test assembly later

If a Runtime / PlayMode test assembly is needed (for example, Phase 10
validation harness):

1. Create `Packages/MidiGenPlay/Tests/Runtime/`.
2. Add an asmdef `MidiGenPlay.Tests.Runtime.asmdef` following the same shape
   as §2 above, with two differences:
   - `includePlatforms: []` and `excludePlatforms: []` (PlayMode-compatible)
   - `references` may omit `MidiGenPlay.Editor` if Runtime tests do not need
     editor code.
3. Append the new assembly name to `testables` in `package.json`:

```json
"testables": [
    "MidiGenPlay.Tests.Editor",
    "MidiGenPlay.Tests.Runtime"
]
```

4. Reimport the package.

## 7. Diagnostic checklist when tests don't appear

In order, stop when something pops:

1. **Does `Packages/MidiGenPlay/package.json` have `testables` listing the
   asmdef name?** Common miss.
2. **Does the asmdef Inspector show all four references resolved (no red
   marks)?** Common cause: a reference name in `references` doesn't match
   the actual `"name"` field of the target asmdef.
3. **Does `Library/ScriptAssemblies/MidiGenPlay.Tests.Editor.dll` exist in
   the consuming project's `Library/` folder?**
   - If yes: the asmdef compiled fine; the issue is test discovery (check
     `testables` again, restart Unity).
   - If no: the asmdef did not compile. Likely a `defineConstraints` issue
     (see §2.1).
4. **Is Test Framework installed?** Window → Package Manager → Unity
   Registry → search "Test Framework".
5. **Nuclear cache reset.** Close Unity. Delete the consuming project's
   `Library/` folder (safe — Unity rebuilds it). Reopen the project.

## 8. Code conventions for new tests

- **Keep tests pure-functional where possible.** Parser tests, math tests,
  data-transform tests work well as automated EditMode tests.
- **UI-coupled flows go in manual smoke procedures**, not automated tests.
  The IMGUI editor window in `DrumPatternEditorWindow` is exercised via the
  SMR3 / SMR6 / SMR7 procedures documented at Phase 7 closure (see the
  Phase 7 changelog entry and the rhythm authoring roadmap).
- **Naming.** `Subject_BehaviorUnderCondition` or `SMR<N>_Subject_Behavior`
  for tests that map to a numbered smoke-test row.
- **Use the public API of the code under test.** `[InternalsVisibleTo]` is
  not currently set on `MidiGenPlay.Runtime` or `MidiGenPlay.Editor`. If a
  test genuinely needs internals access, add the attribute to the target
  asmdef's `AssemblyInfo.cs` (creating it if needed) before relying on it,
  and update this how-to.

## 9. Related authorities

- `SSoT_INDEX.md` — authority order
- `planning/active/Roadmap_Rhythm_Authoring_MVP.md` Phase 10 — validation
  and regression coverage roadmap
- Phase 7 entry in `changelog-ssot.md` — origin batch for this document

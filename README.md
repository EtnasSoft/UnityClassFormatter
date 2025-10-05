# UnityClassFormatter for Unity

Automatic C# code formatter that reorganizes class members following style conventions for Unity projects.

## What does it do?

UnityClassFormatter automatically reorders the members of your C# classes in Unity, grouping them by type and access level, while **preserving `[Header]` groupings** to not disperse related properties in the Inspector.

# UnityClassFormatter (Git Submodule)

This repository is included as a **Git submodule** rather than being copied into the project.
The goal is to avoid duplicating this external tool across multiple Unity projects while keeping it available locally.

## Why a submodule?

- Prevents storing 20MB of binaries in this repository.
- Keeps the formatter versioned and reusable across multiple projects.
- Does not affect the Unity asset pipeline.
- Can be updated independently when needed.

## Location

The submodule is located under:
`Tools/UnityClassFormatter`

This keeps it clearly separated from `Assets/` and from any game-specific code.

## Cloning the project

When cloning the main repository, run:

`git submodule update --init --recursive`

or clone directly with:

`git clone --recursive <repo-url>`

## Updating the submodule

To pull the latest changes from the remote formatter repository:

```
cd Tools/UnityClassFormatter
git pull origin main # or the relevant branch
cd ../..
git commit -am "Update UnityClassFormatter submodule"
```

## Usage

VSCode or other tools can reference the executable directly from:

`Tools/UnityClassFormatter/bin/Publish/UnityClassFormatter.exe`

No files from the submodule are included in the Unity build.

## Sorting Rules

### 1. Section Order

Members are organized in this order:

1. **Constant Fields** (constant fields)
2. **Static Fields** (static fields)
3. **Fields** (instance fields)
4. **Constructors** (constructors)
5. **Properties** (properties)
6. **Events / Delegates** (events and delegates)
7. **Lifecycle Methods** (Unity lifecycle methods)
   - `Awake`
   - `OnEnable`
   - `OnDisable`
   - `OnDestroy`
8. **Public Methods** (public methods)
9. **Private Methods** (private methods)
10. **Nested Types** (nested types)

### 2. Order within each section

Within each section, members are sorted by:

1. **Access level:**

   - `public`
   - `internal`
   - `protected`
   - `private`

2. ** `[Header]` group:** Fields/properties with the same `[Header]` are kept together

3. ** `[Header]` position:** The field that has the `[Header]` attribute always goes first in its group

4. **Alphabetical order:** Within each group, they are sorted alphabetically by name

### 3. Special handling of `[Header]`

The formatter **respects Unity groupings**:

```csharp
// BEFORE (unordered)
[Header("General Settings")]
[SerializeField] private float radius = 0f;
[SerializeField] private float startMinSpeed = 0f;
[SerializeField] private float startMaxSpeed = 0.3f;
[SerializeField] private float gravityModifier = 0f;

// AFTER (alphabetically ordered within the group)
[Header("General Settings")]
[SerializeField] private float radius = 0f;          // ← Has [Header], goes first
[SerializeField] private float gravityModifier = 0f;  // ↓
[SerializeField] private float startMaxSpeed = 0.3f;  // ↓ Ordered
[SerializeField] private float startMinSpeed = 0f;   // ↓ alphabetically
```

Fields remain grouped and are not scattered throughout the class.

## Usage in VSCode

The formatter runs automatically from VSCode via a configured command:

```json
"commands": [
    {
        "cmd": "Formatter\\UnityClassFormatter\\bin\\publish\\UnityClassFormatter.exe \"${file}\"",
        "match": "\\.cs$"
    }
]
```

When executed, it reformats the current `.cs` file automatically.

## Modifying the Formatter

### File to edit

The source code is in:

```
Formatter/UnityClassFormatter/UnityClassFormatter.cs
```

### Recompile after changes

1. Open a terminal in the project folder (where the `.csproj` is)
2. Run:
   ```bash
   dotnet publish src/UnityClassFormatter/UnityClassFormatter.csproj --configuration Release --output bin/publish
   ```
3. This generates the updated files:
   - `bin/publish/UnityClassFormatter.exe`
   - `bin/publish/UnityClassFormatter.pdb`

### Project Structure

```
Formatter/
└── UnityClassFormatter/
    ├── UnityClassFormatter.cs          ← Source code (edit here)
    ├── UnityClassFormatter.csproj      ← Project configuration
    └── bin/
        └── publish/
            ├── UnityClassFormatter.exe ← Executable (generated)
            └── UnityClassFormatter.pdb ← Debug symbols
```

## Technical Requirements

- **.NET 9.0 SDK** to compile
- **Microsoft.CodeAnalysis.CSharp 4.14.0** (included as dependency)

## Important Notes

- The formatter **does not** modify the content of methods, only reorganizes their position
- Comments and attributes are kept with their respective members
- If a field has no `[Header]`, it is sorted alphabetically in its corresponding access level
- Lifecycle methods (`Awake`, `OnEnable`, etc.) are kept in their special section regardless of their visibility

## Full Example

**Before:**

```csharp
public class PlayerController : MonoBehaviour {
    private void Update() { }

    [Header("Movement")]
    [SerializeField] private float speed;
    [SerializeField] private float jumpForce;

    private Rigidbody rb;

    public void Jump() { }

    private void Start() { }

    private void Awake() { }

    [Header("Audio")]
    [SerializeField] private AudioClip walkSound;
}
```

**After:**

```csharp
public class PlayerController : MonoBehaviour {
    [Header("Audio")]
    [SerializeField] private AudioClip walkSound;

    [Header("Movement")]
    [SerializeField] private float jumpForce;
    [SerializeField] private float speed;

    private Rigidbody rb;

    private void Awake() { }

    private void Start() { }

    public void Jump() { }

    private void Update() { }
}
```

## Unit Tests

This project includes unit tests to verify the correctness of the class reordering functionality. The tests are located in the `tests/UnityClassFormatter.Tests/` directory and use the xUnit testing framework.

### Running the Tests

To run the unit tests, ensure you have the .NET SDK installed and execute the following command from the project root:

```bash
dotnet test
```

### What the Tests Cover

The unit tests validate various aspects of the formatter's behavior, including:

- Basic reordering of class members (fields and methods) by access level and alphabetical order.
- Handling of `[Header]` attributes to preserve Unity Inspector groupings.
- Reordering within multiple header groups.
- Sorting of fields with and without headers, ensuring grouped fields remain together.
- Placeholder tests for main program functionality (e.g., file handling).

These tests ensure that the formatter correctly reorganizes C# classes without altering the logic or content of the code, while respecting Unity-specific attributes.

---

## Support

For changes in formatting rules, edit the file `UnityClassFormatter.cs` and adjust the logic in:

- `VisitClassDeclaration()`: Define the categories
- `SortMembers()`: Define the sorting order
- `GetAccessOrder()`: Define the access level priority

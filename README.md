# ClassFormatter for Unity

Automatic C# code formatter that reorganizes class members following style conventions for Unity projects.

## What does it do?

ClassFormatter automatically reorders the members of your C# classes in Unity, grouping them by type and access level, while **preserving `[Header]` groupings** to not disperse related properties in the Inspector.

# ClassFormatter (Git Submodule)

This repository is included as a **Git submodule** rather than being copied into the project.
The goal is to avoid duplicating this external tool across multiple Unity projects while keeping it available locally.

## Why a submodule?

- Prevents storing 20MB of binaries in this repository.
- Keeps the formatter versioned and reusable across multiple projects.
- Does not affect the Unity asset pipeline.
- Can be updated independently when needed.

## Location

The submodule is located under:
`Tools/ClassFormatter`

This keeps it clearly separated from `Assets/` and from any game-specific code.

## Cloning the project

When cloning the main repository, run:

`git submodule update --init --recursive`

or clone directly with:

`git clone --recursive <repo-url>`

## Updating the submodule

To pull the latest changes from the remote formatter repository:

```
cd Tools/ClassFormatter
git pull origin main # or the relevant branch
cd ../..
git commit -am "Update ClassFormatter submodule"
```

## Usage

VSCode or other tools can reference the executable directly from:

`Tools/ClassFormatter/bin/Publish/ClassFormatter.exe`

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
        "cmd": "Formatter\\ClassFormatter\\bin\\publish\\ClassFormatter.exe \"${file}\"",
        "match": "\\.cs$"
    }
]
```

When executed, it reformats the current `.cs` file automatically.

## Modifying the Formatter

### File to edit

The source code is in:

```
Formatter/ClassFormatter/ClassFormatter.cs
```

### Recompile after changes

1. Open a terminal in the project folder (where the `.csproj` is)
2. Run:
   ```bash
   dotnet publish src/ClassFormatter/ClassFormatter.csproj --configuration Release --output bin/publish
   ```
3. This generates the updated files:
   - `bin/publish/ClassFormatter.exe`
   - `bin/publish/ClassFormatter.pdb`

### Project Structure

```
Formatter/
└── ClassFormatter/
    ├── ClassFormatter.cs          ← Source code (edit here)
    ├── ClassFormatter.csproj      ← Project configuration
    └── bin/
        └── publish/
            ├── ClassFormatter.exe ← Executable (generated)
            └── ClassFormatter.pdb ← Debug symbols
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

---

## Support

For changes in formatting rules, edit the file `ClassFormatter.cs` and adjust the logic in:

- `VisitClassDeclaration()`: Define the categories
- `SortMembers()`: Define the sorting order
- `GetAccessOrder()`: Define the access level priority


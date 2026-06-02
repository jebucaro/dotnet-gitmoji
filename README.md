# dotnet-gitmoji

[![NuGet](https://img.shields.io/nuget/v/dotnet-gitmoji?style=flat-square)](https://www.nuget.org/packages/dotnet-gitmoji)
[![Downloads](https://img.shields.io/nuget/dt/dotnet-gitmoji?style=flat-square)](https://www.nuget.org/packages/dotnet-gitmoji)
[![License](https://img.shields.io/github/license/jebucaro/dotnet-gitmoji?style=flat-square)](LICENSE)

`dotnet-gitmoji` brings the [gitmoji](https://gitmoji.dev) commit convention to .NET projects. It installs a
`prepare-commit-msg` hook through [Husky.Net](https://alirezanet.github.io/Husky.Net/) so the hook travels with the
repo. When a teammate clones the repo and runs `dotnet tool restore`, the hook is ready on their first commit.

---

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later
- [Git for Windows](https://git-scm.com/download/win) (Windows only; the hook runs under Git Bash)

---

## First-developer setup

Run these steps once in your repo. After you commit the generated files, every teammate gets the hook automatically.

**Step 1: Add both tools to the local tool manifest**

```sh
dotnet new tool-manifest   # skip if .config/dotnet-tools.json already exists
dotnet tool install Husky
dotnet tool install dotnet-gitmoji
```

This creates `.config/dotnet-tools.json`, which pins both tool versions for the whole team.

**Step 2: Initialize Husky.Net**

```sh
dotnet tool run husky install
```

Sets `core.hooksPath` to `.husky/` and creates the `.husky/` directory with the Husky.Net helper scripts.

**Step 3: Install the gitmoji hook**

```sh
dotnet tool run dotnet-gitmoji init --mode shell
```

Adds a `prepare-commit-msg` hook to `.husky/`. The hook file is plain text and safe to commit.

**Step 4: Add the MSBuild target**

Create `Directory.Build.targets` at the repo root:

```xml
<Project>
  <PropertyGroup>
    <HuskyRoot Condition="'$(HuskyRoot)' == ''">$(MSBuildThisFileDirectory)</HuskyRoot>
  </PropertyGroup>
  <Target Name="Husky" AfterTargets="Restore" Condition="'$(HUSKY)' != 0"
          Inputs="$(HuskyRoot).config/dotnet-tools.json"
          Outputs="$(HuskyRoot).husky/_/install.stamp">
    <Exec Command="dotnet tool restore"
          StandardOutputImportance="Low" StandardErrorImportance="High" />
    <Exec Command="dotnet husky install"
          StandardOutputImportance="Low" StandardErrorImportance="High"
          WorkingDirectory="$(HuskyRoot)" />
    <Touch Files="$(HuskyRoot).husky/_/install.stamp" AlwaysCreate="true"
           Condition="Exists('$(HuskyRoot).husky/_')" />
    <ItemGroup>
      <FileWrites Include="$(HuskyRoot).husky/_/install.stamp" />
    </ItemGroup>
  </Target>
</Project>
```

This target runs `dotnet tool restore` and `dotnet husky install` automatically whenever a teammate restores or
opens the project in Visual Studio or Rider. The `Inputs`/`Outputs` pair gives MSBuild incremental build support:
the target re-runs only when `.config/dotnet-tools.json` changes (a tool version bump) or after `dotnet clean`.
The stamp file it creates lives in `.husky/_/`, which is already gitignored by Husky.Net, so no `.gitignore`
entry is needed.

> [!TIP]
> If your repo has a single `.csproj`, you can skip creating `Directory.Build.targets` manually. Run
> `dotnet tool run husky attach <path-to.csproj>` instead. Husky.Net adds the same target directly to your
> project file.

> [!NOTE]
> Set the `HUSKY` environment variable to `0` in your CI environment to skip hook installation in pipelines.

**Step 5: Commit everything**

```sh
git add .config/dotnet-tools.json .husky/ Directory.Build.targets
git commit -m "chore: add dotnet-gitmoji hook"
```

That is all the first developer needs to do. Teammates get the hook without any manual steps.

---

## Joining a repo

If the repo already has dotnet-gitmoji set up (it has a `.husky/` directory and `Directory.Build.targets`), run:

```sh
dotnet restore
```

Or simply open the project in Visual Studio or Rider. Both trigger the MSBuild target in `Directory.Build.targets`,
which runs `dotnet tool restore` and `dotnet husky install` in sequence. `dotnet husky install` does two things:
sets `core.hooksPath` to `.husky/` so git finds the committed hook, and creates the `.husky/_/` helper directory
that the hook script sources at runtime.

Your next `git commit` opens the gitmoji prompt.

> [!IMPORTANT]
> You must run `dotnet restore` (NuGet package restore) before your first commit, not `dotnet tool restore`.
> `dotnet tool restore` only installs .NET tools and does not trigger MSBuild. Without MSBuild running
> `dotnet husky install`, `core.hooksPath` is never set, git looks in `.git/hooks/` instead of `.husky/`,
> and the gitmoji prompt silently does not appear.

If the repo has no project file (no `.csproj`) to trigger NuGet restore, run the two steps manually instead:

```sh
dotnet tool restore
dotnet tool run husky install
```

To remove the hook:

```sh
dotnet tool run dotnet-gitmoji remove
```

> [!NOTE]
> `remove` prints manual cleanup instructions for Husky.Net-managed hooks rather than editing `.husky/` directly.
> Follow the printed steps to fully detach the hook.

---

## Usage

### Hook mode

After setup, commit normally:

```sh
git commit
git commit -m "fix login redirect"   # message pre-filled as the title suggestion
```

When you pass `-m`, the value is offered as a pre-filled title at the gitmoji prompt. The hook skips itself during
merge commits, squash merges, amends, and interactive rebases so automated flows are not interrupted.

### Client mode

Use `dotnet-gitmoji commit` as a drop-in for `git commit` when you prefer not to install a hook:

```sh
dotnet tool run dotnet-gitmoji commit
dotnet tool run dotnet-gitmoji commit --title "fix login redirect"
dotnet tool run dotnet-gitmoji commit --title "fix login redirect" --scope auth --message "Resolves #42"
```

> [!NOTE]
> Client mode is disabled when a hook is already installed, to prevent the emoji from being applied twice.

| Option      | Short | Description                           |
|-------------|-------|---------------------------------------|
| `--title`   | `-t`  | Commit title (skips the title prompt) |
| `--scope`   | `-s`  | Commit scope (e.g. `feat(auth): ...`) |
| `--message` | `-m`  | Commit message body                   |

---

## Configuration

### Interactive wizard

```sh
dotnet tool run dotnet-gitmoji config
```

Walks through every option and saves to `.gitmojirc.json` in the repo root (creating it if absent). Use `--global` to
save to the personal global config (`~/.dotnet-gitmoji/config.json`) instead.

### Manual configuration

Create `.gitmojirc.json` in your repo root, or generate one with defaults:

```sh
dotnet tool run dotnet-gitmoji init --mode shell --config
```

Example file:

```json
{
  "emojiFormat": "Emoji",
  "scopePrompt": false,
  "messagePrompt": false,
  "capitalizeTitle": true,
  "maxTitleLength": 48,
  "trimTitleWhenExceeded": true,
  "gitmojisUrl": "https://gitmoji.dev/api/gitmojis",
  "autoAdd": false,
  "signedCommit": false,
  "scopes": null,
  "enforceConvention": false
}
```

### Configuration reference

| Key                 | Type                  | Default                            | Description                                                                                                 |
|---------------------|-----------------------|------------------------------------|-------------------------------------------------------------------------------------------------------------|
| `emojiFormat`       | `"Emoji"` \| `"Code"` | `"Emoji"`                          | Prefix with the emoji character (`🐛`) or its shortcode (`:bug:`)                                           |
| `scopePrompt`       | `bool`                | `false`                            | Prompt for a commit scope (e.g. `feat(auth): ...`)                                                          |
| `messagePrompt`     | `bool`                | `false`                            | Prompt for an optional commit message body                                                                  |
| `capitalizeTitle`   | `bool`                | `true`                             | Automatically capitalize the first letter of the commit title                                               |
| `maxTitleLength`    | `int` \| `null`       | `48`                               | Maximum allowed commit title length; set to `null` to disable length enforcement                           |
| `trimTitleWhenExceeded` | `bool`            | `true`                             | In interactive prompts, automatically trim titles longer than `maxTitleLength` at a word boundary          |
| `gitmojisUrl`       | `string`              | `https://gitmoji.dev/api/gitmojis` | URL to fetch the gitmoji list from                                                                          |
| `autoAdd`           | `bool`                | `false`                            | Stage all changes before committing (client mode only)                                                      |
| `signedCommit`      | `bool`                | `false`                            | Sign commits with GPG (client mode only)                                                                    |
| `scopes`            | `string[]` \| `null`  | `null`                             | Predefined scope suggestions shown when `scopePrompt` is `true`                                             |
| `enforceConvention` | `bool`                | `false`                            | Reject commits that don't start with a gitmoji when no interactive terminal is available (e.g. IDE commits) |

### Config resolution order

| Location                        | Purpose                                        |
|---------------------------------|------------------------------------------------|
| `.gitmojirc.json` in repo root  | Shared team settings; commit this to your repo |
| `~/.dotnet-gitmoji/config.json` | Personal global fallback                       |
| Built-in defaults               | Applied when no config file exists             |

---

## Command reference

| Command                                  | Description                                                                                          |
|------------------------------------------|------------------------------------------------------------------------------------------------------|
| `dotnet-gitmoji init --mode shell`       | Install the `prepare-commit-msg` hook via Husky.Net (shell mode)                                     |
| `dotnet-gitmoji init --mode task-runner` | Install the hook via Husky.Net's task runner                                                         |
| `dotnet-gitmoji remove`                  | Uninstall the hook (prints manual cleanup steps for Husky.Net-managed hooks)                         |
| `dotnet-gitmoji commit`                  | Interactive commit (client mode)                                                                     |
| `dotnet-gitmoji config`                  | Run the configuration wizard (saves to repo config by default; use `--global` for personal settings) |
| `dotnet-gitmoji list`                    | List all available gitmojis                                                                          |
| `dotnet-gitmoji search <keyword>`        | Fuzzy-search gitmojis by name, code, or description                                                  |
| `dotnet-gitmoji update`                  | Refresh the cached gitmoji list from the remote API                                                  |

When installed locally, prefix every command with `dotnet tool run`:

```sh
dotnet tool run dotnet-gitmoji init --mode shell
dotnet tool run dotnet-gitmoji commit
```

---

## Other installation paths

### Global install

Global install is convenient for personal use across repos. It does not share the tool with teammates; each
teammate would need to install it globally themselves. For shared projects, use the local install path above.

```sh
dotnet tool install --global Husky
dotnet tool install --global dotnet-gitmoji
husky install
dotnet-gitmoji init --mode shell
```

Then add `Directory.Build.targets` (same content as the local variant) and commit `.husky/` and
`Directory.Build.targets`. Note that `.config/dotnet-tools.json` is not involved for global installs.

### Task-runner mode

If your repo already uses Husky.Net's task runner, use `--mode task-runner` instead of `--mode shell`. This adds a
`dotnet-gitmoji` entry to `.husky/task-runner.json` and registers the hook to invoke it:

```sh
dotnet tool run dotnet-gitmoji init --mode task-runner
```

Both modes produce the same prompt experience. Use shell mode unless you already have a `task-runner.json` with
other tasks in it.

---

## License

MIT, [Jonathan Búcaro](https://github.com/jebucaro)

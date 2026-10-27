# dotnet-gitmoji

[![NuGet](https://img.shields.io/nuget/v/dotnet-gitmoji?style=flat-square)](https://www.nuget.org/packages/dotnet-gitmoji)
[![Downloads](https://img.shields.io/nuget/dt/dotnet-gitmoji?style=flat-square)](https://www.nuget.org/packages/dotnet-gitmoji)
[![License](https://img.shields.io/github/license/jebucaro/dotnet-gitmoji?style=flat-square)](LICENSE)

`dotnet-gitmoji` brings the [gitmoji](https://gitmoji.dev) commit convention to .NET projects. It installs a
`prepare-commit-msg` hook through [Husky.Net](https://alirezanet.github.io/Husky.Net/) so the hook travels with the
repo. When a teammate clones the repo and runs `dotnet restore` (or opens the project in an IDE), the hook is ready on
their first commit.

---

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later
- [Git for Windows](https://git-scm.com/download/win) (Windows only; the hook runs under Git Bash)

> [!NOTE]
> Emojis in `list` and `search` render in color only when your terminal has a color-emoji font
> in its fallback chain. Terminals like Alacritty that lack one show monochrome glyphs instead.
> That is a local font setting, not a tool issue, and the columns stay aligned either way.

---

## First-developer setup

Run these steps once in your repo. After you commit the generated files, every teammate gets the hook automatically.

**Step 1: Add both tools to the local tool manifest**

```sh
dotnet new tool-manifest   # skip if the repo already has a dotnet-tools.json
dotnet tool install Husky
dotnet tool install dotnet-gitmoji
```

This creates `dotnet-tools.json` at the repo root, which pins both tool versions for the whole team. (SDKs before
.NET 10 created it as `.config/dotnet-tools.json`; both locations work with the steps below.)

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
    <HuskyToolManifest Condition="'$(HuskyToolManifest)' == '' and Exists('$(HuskyRoot)dotnet-tools.json')">$(HuskyRoot)dotnet-tools.json</HuskyToolManifest>
    <HuskyToolManifest Condition="'$(HuskyToolManifest)' == ''">$(HuskyRoot).config/dotnet-tools.json</HuskyToolManifest>
  </PropertyGroup>
  <Target Name="Husky" BeforeTargets="Restore;CollectPackageReferences" Condition="'$(HUSKY)' != 0"
          Inputs="$(HuskyToolManifest)"
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

This target runs `dotnet tool restore` and `dotnet husky install` whenever a teammate restores, builds, or opens the
project in Visual Studio or Rider. It re-runs only when `dotnet-tools.json` changes or after `dotnet clean`; its stamp
file lives in the already-gitignored `.husky/_/`. Set `HUSKY=0` in CI to skip it.

> [!WARNING]
> Don't use `dotnet tool run husky attach <path-to.csproj>` instead. Husky.Net 0.9.1 writes a target that does not
> fire on a solution-level `dotnet restore` and hardcodes `.config/dotnet-tools.json`, which .NET 10 no longer creates.

<details>
<summary>Why <code>BeforeTargets</code>, and a caveat for private feeds</summary>

When you restore a solution (`.sln` or `.slnx`), NuGet restores every project from the solution level and never runs
each project's own `Restore` target, so an `AfterTargets="Restore"` hook silently never fires.
`CollectPackageReferences` runs for every project during both restore and build, so the hook fires either way.

The trade-off: on private NuGet feeds that authenticate through a credential helper, `dotnet tool restore` can run
before the helper has signed in and fail the restore. If that happens, affected developers set `HUSKY=0` and run
`dotnet tool restore` and `dotnet tool run husky install` once by hand after cloning. See
[Husky.Net #176](https://github.com/alirezanet/Husky.Net/issues/176).

</details>

**Step 5: Commit everything**

```sh
git add dotnet-tools.json .husky/ Directory.Build.targets   # or .config/dotnet-tools.json on older SDKs
git commit -m "chore: add dotnet-gitmoji hook"
```

That is all the first developer needs to do. Teammates get the hook without any manual steps.

---

## Joining a repo

If the repo already has dotnet-gitmoji set up (it has a `.husky/` directory and `Directory.Build.targets`), run:

```sh
dotnet restore
```

Or open the project in Visual Studio or Rider. Either one triggers the MSBuild target, which restores the tools and
runs `dotnet husky install` to point `core.hooksPath` at `.husky/`. Your next `git commit` opens the gitmoji prompt.

> [!IMPORTANT]
> Use `dotnet restore`, not `dotnet tool restore`. The latter only installs tools and never runs MSBuild, so
> `core.hooksPath` is never set and the gitmoji prompt silently does not appear.

If the repo has no project file to trigger a restore, run the two steps manually instead:

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

When you pass `-m`, the value is offered as a pre-filled title at the gitmoji prompt. The hook skips itself during merge
commits, squash merges, amends, and interactive rebases so automated flows are not interrupted.

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

Walks through every option and saves to `.gitmojirc.json` in the repo root (creating it if absent). The final
question, the color theme, is saved to your personal global config instead (see [Themes](#themes)).

### Manual configuration

Create `.gitmojirc.json` in your repo root by hand, or generate one with every default filled in:

```sh
dotnet tool run dotnet-gitmoji init --mode shell --config
```

Commit it so every contributor uses the same convention. When the file is absent, the built-in defaults apply.

### Configuration reference

| Key                     | Type                  | Default                            | Description                                                                                                     |
|-------------------------|-----------------------|------------------------------------|-----------------------------------------------------------------------------------------------------------------|
| `emojiFormat`           | `"Emoji"` \| `"Code"` | `"Emoji"`                          | Prefix with the emoji character (`🐛`) or its shortcode (`:bug:`)                                               |
| `scopePrompt`           | `bool`                | `false`                            | Prompt for a commit scope (e.g. `feat(auth): ...`)                                                              |
| `messagePrompt`         | `bool`                | `false`                            | Prompt for an optional commit message body                                                                      |
| `capitalizeTitle`       | `bool`                | `true`                             | Automatically capitalize the first letter of the commit title                                                   |
| `maxTitleLength`        | `int` \| `null`       | `48`                               | Maximum allowed commit title length; set to `null` to disable length enforcement                                |
| `trimTitleWhenExceeded` | `bool`                | `true`                             | In interactive prompts, automatically trim titles longer than `maxTitleLength` at a word boundary               |
| `gitmojisUrl`           | `string`              | `https://gitmoji.dev/api/gitmojis` | URL to fetch the gitmoji list from                                                                              |
| `autoAdd`               | `bool`                | `false`                            | Stage all changes before committing (client mode only)                                                          |
| `signedCommit`          | `bool`                | `false`                            | Sign commits with GPG (client mode only)                                                                        |
| `scopes`                | `string[]` \| `null`  | `null`                             | Predefined scope suggestions shown when `scopePrompt` is `true`                                                 |
| `enforceConvention`     | `bool`                | `false`                            | Reject commits that don't start with a gitmoji when no interactive terminal is available (e.g. IDE commits)     |
| `showSemverBadge`       | `bool`                | `true`                             | Show the semver badge next to each gitmoji in the interactive picker, list, and search results                  |
| `normalizeCommitFormat` | `bool`                | `false`                            | Produce `emoji: title` (with colon) instead of `emoji title`, aligning with the conventional commit format      |
| `theme`                 | `string`              | `"default"`                        | Color theme for all tool output; **personal setting, not read from this file**, see [Themes](#themes)           |

### Themes

The theme selects the color palette used by the interactive picker, prompts, and command output. Because
`.gitmojirc.json` is shared with your whole team, the theme is treated as a **personal setting** and is resolved from
personal sources only, in this order:

1. `DOTNET_GITMOJI_THEME` environment variable
2. `theme` in your personal global config (`~/.dotnet-gitmoji/config.json`)
3. Built-in `default`

A `theme` key in a repo's `.gitmojirc.json` is ignored, and any key other than `theme` in the global config is ignored;
both cases print a note on stderr.

| Theme                                                           | Best on                                    |
|-----------------------------------------------------------------|--------------------------------------------|
| `default`                                                       | Any (follows your terminal's ANSI palette) |
| `monokai`                                                       | Dark backgrounds                           |
| `catppuccin-latte`                                              | Light backgrounds                          |
| `catppuccin-frappe`, `catppuccin-macchiato`, `catppuccin-mocha` | Dark backgrounds                           |

Themes set foreground colors only, so your terminal's background and transparency are always preserved. `default` uses
named ANSI colors, so it adapts to whatever color scheme your terminal is configured with; the named themes use exact
RGB values, which Spectre.Console automatically downgrades on terminals without truecolor support. The `NO_COLOR`
environment variable is honored (all colors are stripped). An unknown theme name falls back to `default` with a warning
on stderr.

---

## Command reference

| Command                                  | Description                                                                                       |
|------------------------------------------|---------------------------------------------------------------------------------------------------|
| `dotnet-gitmoji init --mode shell`       | Install the `prepare-commit-msg` hook via Husky.Net (shell mode)                                  |
| `dotnet-gitmoji init --mode task-runner` | Install the hook via Husky.Net's task runner                                                      |
| `dotnet-gitmoji remove`                  | Uninstall the hook (prints manual cleanup steps for Husky.Net-managed hooks)                      |
| `dotnet-gitmoji commit`                  | Interactive commit (client mode)                                                                  |
| `dotnet-gitmoji config`                  | Run the configuration wizard (saves to the repo config; theme goes to the personal global config) |
| `dotnet-gitmoji list`                    | List all available gitmojis                                                                       |
| `dotnet-gitmoji search <keyword>`        | Fuzzy-search gitmojis by name, code, or description                                               |
| `dotnet-gitmoji update`                  | Refresh the cached gitmoji list from the remote API                                               |

For a local install, prefix each command with `dotnet tool run`.

---

## Other installation paths

### Global install

Global install is convenient for personal use across repos. It does not share the tool with teammates; each teammate
would need to install it globally themselves. For shared projects, use the local install path above.

```sh
dotnet tool install --global Husky
dotnet tool install --global dotnet-gitmoji
husky install
dotnet-gitmoji init --mode shell
```

Then add a `Directory.Build.targets` at the repo root and commit it together with `.husky/`. The local variant's
target doesn't work here: there is no tool manifest to restore, and a global Husky.Net is invoked as `husky`, not
`dotnet husky`. Use this instead:

```xml
<Project>
  <PropertyGroup>
    <HuskyRoot Condition="'$(HuskyRoot)' == ''">$(MSBuildThisFileDirectory)</HuskyRoot>
  </PropertyGroup>
  <Target Name="Husky" BeforeTargets="Restore;CollectPackageReferences"
          Condition="'$(HUSKY)' != 0 and !Exists('$(HuskyRoot).husky/_/install.stamp')">
    <Exec Command="husky install"
          StandardOutputImportance="Low" StandardErrorImportance="High"
          WorkingDirectory="$(HuskyRoot)" />
    <Touch Files="$(HuskyRoot).husky/_/install.stamp" AlwaysCreate="true"
           Condition="Exists('$(HuskyRoot).husky/_')" />
  </Target>
</Project>
```

Every teammate must also install both tools globally, or their restore fails when it runs `husky install`.

### Task-runner mode

If your repo already uses Husky.Net's task runner, use `--mode task-runner` instead of `--mode shell`. This adds a
`dotnet-gitmoji` entry to `.husky/task-runner.json` and registers the hook to invoke it:

```sh
dotnet tool run dotnet-gitmoji init --mode task-runner
```

Both modes produce the same prompt experience. Use shell mode unless you already have a `task-runner.json` with other
tasks in it.

---

## License

MIT, [Jonathan Búcaro](https://github.com/jebucaro)

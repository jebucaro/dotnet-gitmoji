# dotnet-gitmoji

[![NuGet](https://img.shields.io/nuget/v/dotnet-gitmoji?style=flat-square)](https://www.nuget.org/packages/dotnet-gitmoji)
[![Downloads](https://img.shields.io/nuget/dt/dotnet-gitmoji?style=flat-square)](https://www.nuget.org/packages/dotnet-gitmoji)
[![License](https://img.shields.io/github/license/jebucaro/dotnet-gitmoji?style=flat-square)](LICENSE)

> Write expressive, emoji-prefixed commit messages in .NET projects — and share the setup with your team via a single
> `dotnet tool restore`.

`dotnet-gitmoji` brings the [gitmoji](https://gitmoji.dev) commit convention to your .NET workflow. It installs a
`prepare-commit-msg` hook through [Husky.Net](https://alirezanet.github.io/Husky.Net/) so the hook lives alongside
your source — when a teammate clones the repo and runs `dotnet tool restore`, the hook is ready on their first
commit. A client mode (`dotnet-gitmoji commit`) is available for one-off use on machines where you don't want a hook.

---

## Features

- 🤝 **Team-friendly** — installs into Husky.Net so the hook ships with the repo; teammates inherit it on `dotnet tool restore`
- 🪝 **Two Husky.Net modes** — shell hook (simplest) or task-runner integration for repos already using it
- 💻 **Client mode** — `dotnet-gitmoji commit` for one-off use without a hook
- 🔍 **Fuzzy search** — find the right emoji by name, code, or description
- ⚙️ **Flexible config** — per-repo `.gitmojirc.json` or a personal global fallback

---

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later
- [Husky.Net](https://alirezanet.github.io/Husky.Net/) — installed as a local tool (recipe in [Quick Start](#quick-start))
- On Windows, the hook script runs under Git Bash, which ships with [Git for Windows](https://git-scm.com/download/win)

---

## Installation

### Local (recommended)

```sh
dotnet new tool-manifest   # skip if .config/dotnet-tools.json already exists
dotnet tool install dotnet-gitmoji
```

Commit `.config/dotnet-tools.json` to your repo. Teammates get the tool with a single `dotnet tool restore` after
cloning — no per-machine install required.

> [!IMPORTANT]
> When installed locally, prefix every command with `dotnet tool run`:
> ```sh
> dotnet tool run dotnet-gitmoji init --mode shell
> dotnet tool run dotnet-gitmoji commit
> ```
> The tool detects local installation automatically and generates the correct hook command.

### Global

```sh
dotnet tool install --global dotnet-gitmoji
```

Use `dotnet-gitmoji <command>` directly anywhere in your terminal. Global install is convenient for personal use
across repos, but it does not share the tool with teammates — prefer the local install for any shared project.

---

## Quick Start

The recommended setup uses a local tool manifest and Husky.Net's shell-mode hook. After these four steps, both you
and any teammate cloning the repo will get the gitmoji prompt on `git commit`.

```sh
# 1. Add Husky.Net and dotnet-gitmoji to your tool manifest
dotnet new tool-manifest
dotnet tool install Husky
dotnet tool install dotnet-gitmoji

# 2. Initialize Husky.Net (creates .husky/, sets core.hooksPath)
dotnet tool run husky install

# 3. Install the gitmoji prepare-commit-msg hook
dotnet tool run dotnet-gitmoji init --mode shell

# 4. Commit as usual — pick an emoji at the prompt
git commit
```

To remove the hook later:

```sh
dotnet tool run dotnet-gitmoji remove
```

> [!NOTE]
> For Husky.Net hooks, `remove` prints manual cleanup instructions instead of editing `.husky/` files itself —
> follow the printed steps to fully detach the hook.

For the full team-onboarding setup (so teammates need only `dotnet tool restore`), see
[Sharing with your team](#sharing-with-your-team).

---

## Usage

### Hook Mode (recommended)

`dotnet-gitmoji init --mode shell` appends a `dotnet-gitmoji` invocation to `.husky/prepare-commit-msg` via
`dotnet husky add`. The file lives in `.husky/` — committed to the repo — so the hook follows the project rather
than the developer's machine.

After init, just commit normally:

```sh
git commit
git commit -m "fix login redirect"   # message pre-filled as title
```

When you pass `-m`, the message is pre-filled as the title suggestion at the prompt. The hook skips itself on merge,
squash, amend, and during interactive rebase, so automated commit flows aren't interrupted.

#### Task-runner mode

If your repo already uses Husky.Net's task runner, use `--mode task-runner` instead. This adds a `dotnet-gitmoji`
task to `.husky/task-runner.json` and registers the hook to invoke it:

```sh
dotnet tool run dotnet-gitmoji init --mode task-runner
```

Both modes produce the same prompt experience — pick whichever matches your existing Husky.Net setup.

### Client Mode

`dotnet-gitmoji commit` works as a drop-in replacement for `git commit` when you prefer not to install a hook.

> [!NOTE]
> Client mode is disabled when a hook is already installed, to prevent the emoji from being applied twice.
> Client mode also runs only on the local machine — it doesn't share anything with teammates.

```sh
dotnet-gitmoji commit
dotnet-gitmoji commit --title "fix login redirect"
dotnet-gitmoji commit --title "fix login redirect" --scope auth --message "Resolves #42"
```

| Option      | Short | Description                           |
|-------------|-------|---------------------------------------|
| `--title`   | `-t`  | Commit title (skips the title prompt) |
| `--scope`   | `-s`  | Commit scope (e.g. `feat(auth): …`)   |
| `--message` | `-m`  | Commit message body                   |

### Sharing with your team

To make the gitmoji hook fire automatically for every teammate after they clone the repo:

1. **Use a local tool manifest.** Run `dotnet new tool-manifest` and install both tools locally so they're listed
   in `.config/dotnet-tools.json`:
   ```sh
   dotnet tool install Husky
   dotnet tool install dotnet-gitmoji
   ```
2. **Add a `post-restore` MSBuild target.** Wire `dotnet tool restore` to also run `dotnet husky install`
   automatically. Husky.Net documents the exact target snippet to use — see
   [Husky.Net: Automatic Husky Install](https://alirezanet.github.io/Husky.Net/guide/automate.html). For multi-project
   solutions, place the target in a `Directory.Build.targets` file at the repo root.
3. **Run `dotnet-gitmoji init --mode shell` once** and commit the resulting `.config/dotnet-tools.json`, `.husky/`
   directory, and the `.csproj` / `Directory.Build.targets` changes from step 2.
4. **Teammates clone and run `dotnet tool restore`.** That single command installs both tools, runs `husky install`,
   and activates the hook. Their next `git commit` opens the gitmoji prompt — no manual setup, no global install.

Direct hooks under `.git/hooks/` cannot do this: `.git/` is never committed, so every teammate would need to
re-run `init` themselves. The Husky.Net path is the only setup that actually transfers across machines.

---

## Configuration

### Interactive wizard

The quickest way to configure preferences — walks through every option and saves to the global config:

```sh
dotnet-gitmoji config
```

### Manual configuration

Create a `.gitmojirc.json` in your repo root, or generate one with defaults by passing `--config` to `init`:

```sh
dotnet-gitmoji init --mode shell --config
```

Example file:

```json
{
  "emojiFormat": "Emoji",
  "scopePrompt": false,
  "messagePrompt": false,
  "capitalizeTitle": true,
  "gitmojisUrl": "https://gitmoji.dev/api/gitmojis",
  "autoAdd": false,
  "signedCommit": false,
  "scopes": null
}
```

### Configuration reference

| Key               | Type                  | Default                            | Description                                                       |
|-------------------|-----------------------|------------------------------------|-------------------------------------------------------------------|
| `emojiFormat`     | `"Emoji"` \| `"Code"` | `"Emoji"`                          | Prefix with the emoji character (`🐛`) or its shortcode (`:bug:`) |
| `scopePrompt`     | `bool`                | `false`                            | Prompt for a commit scope (e.g. `feat(auth): …`)                  |
| `messagePrompt`   | `bool`                | `false`                            | Prompt for an optional commit message body                        |
| `capitalizeTitle` | `bool`                | `true`                             | Automatically capitalize the first letter of the commit title     |
| `gitmojisUrl`     | `string`              | `https://gitmoji.dev/api/gitmojis` | URL to fetch the gitmoji list from                                |
| `autoAdd`         | `bool`                | `false`                            | Stage all changes before committing *(client mode only)*          |
| `signedCommit`    | `bool`                | `false`                            | Sign commits with GPG (`git commit -S`) *(client mode only)*      |
| `scopes`          | `string[]` \| `null`  | `null`                             | Predefined scope suggestions shown when `scopePrompt` is `true`   |

### Config resolution order

The tool looks for configuration in this order, using the first match:

| Location                                                 | Purpose                                         |
|----------------------------------------------------------|-------------------------------------------------|
| `.gitmojirc.json` in repo root (or any parent directory) | Shared team settings — commit this to your repo |
| `~/.dotnet-gitmoji/config.json`                          | Personal global fallback                        |
| Built-in defaults                                        | Applied when no config file exists              |

---

## Commands

| Command                           | Description                                                                          |
|-----------------------------------|--------------------------------------------------------------------------------------|
| `dotnet-gitmoji init --mode shell` | Install the `prepare-commit-msg` hook via Husky.Net (shell mode)                    |
| `dotnet-gitmoji init --mode task-runner` | Install the hook via Husky.Net's task runner                                  |
| `dotnet-gitmoji remove`           | Uninstall the hook (prints manual cleanup steps for Husky.Net-managed hooks)         |
| `dotnet-gitmoji commit`           | Interactive commit (client mode)                                                     |
| `dotnet-gitmoji config`           | Run the configuration wizard                                                         |
| `dotnet-gitmoji list`             | List all available gitmojis                                                          |
| `dotnet-gitmoji search <keyword>` | Fuzzy-search gitmojis by name, code, or description                                  |
| `dotnet-gitmoji update`           | Refresh the cached gitmoji list from the remote API                                  |

---

## License

MIT — [Jonathan Búcaro](https://github.com/jebucaro)

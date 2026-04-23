# dotnet-gitmoji

[![NuGet](https://img.shields.io/nuget/v/dotnet-gitmoji?style=flat-square)](https://www.nuget.org/packages/dotnet-gitmoji)
[![Downloads](https://img.shields.io/nuget/dt/dotnet-gitmoji?style=flat-square)](https://www.nuget.org/packages/dotnet-gitmoji)
[![License](https://img.shields.io/github/license/jebucaro/dotnet-gitmoji?style=flat-square)](LICENSE)

> Write expressive, emoji-prefixed commit messages in .NET projects — automatically via a Git hook, or interactively
> from the terminal.

`dotnet-gitmoji` brings the [gitmoji](https://gitmoji.dev) commit convention to your .NET workflow. Pick an emoji, type
your message, and commit — the tool handles the format. It works either as a **Git hook** that intercepts every
`git commit`, or as a **standalone command** that replaces `git commit`.

---

## Features

- 🪝 **Git hook mode** — installs a `prepare-commit-msg` hook that activates on every commit
- 💻 **Client mode** — use `dotnet-gitmoji commit` as a drop-in for `git commit`
- 🔍 **Fuzzy search** — find the right emoji by name, code, or description
- ⚙️ **Flexible config** — per-repo `.gitmojirc.json` or a personal global fallback
- 🤝 **Husky.Net support** — integrates with both shell and task-runner setups
- 📦 **Local & global install** — generates the correct hook command for either setup

---

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later

---

## Installation

### Global

```sh
dotnet tool install --global dotnet-gitmoji
```

Use `dotnet-gitmoji <command>` directly anywhere in your terminal.

### Local (per project)

```sh
dotnet new tool-manifest   # skip if .config/dotnet-tools.json already exists
dotnet tool install dotnet-gitmoji
```

> [!IMPORTANT]
> When installed locally, prefix every command with `dotnet tool run`:
> ```sh
> dotnet tool run dotnet-gitmoji init
> dotnet tool run dotnet-gitmoji commit
> ```
> The tool detects local installation automatically and generates the correct hook command.

---

## Quick Start

Install the Git hook and start committing with gitmoji in three steps:

```sh
# 1. Install the hook
dotnet-gitmoji init

# 2. Commit as usual — the hook activates automatically
git commit

# 3. Pick your emoji, confirm the message, done ✓
```

To remove the hook at any time:

```sh
dotnet-gitmoji remove
```

---

## Usage

### Hook Mode (recommended)

Hook mode installs a `prepare-commit-msg` hook that runs every time you call `git commit`. The tool prompts you to pick
a gitmoji, and any commit message you passed (via `-m` or your editor) is pre-filled as the title suggestion.

```sh
dotnet-gitmoji init
```

After that, just commit normally:

```sh
git commit
git commit -m "fix login redirect"   # message pre-filled as title
```

### Client Mode

`dotnet-gitmoji commit` works as a drop-in replacement for `git commit` when you prefer not to use the hook.

> [!NOTE]
> Client mode is disabled when the hook is already installed, to prevent the emoji from being applied twice.

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

---

## Husky.Net Integration

Already using [Husky.Net](https://alirezanet.github.io/Husky.Net/)? Pass `--mode` to `init` and `dotnet-gitmoji` will
integrate with your existing Husky setup instead of creating a standalone hook.

### Shell mode

Appends the gitmoji invocation to your `.husky/prepare-commit-msg` file:

```sh
dotnet-gitmoji init --mode shell
```

### Task-runner mode

Adds a `dotnet-gitmoji` task to `.husky/task-runner.json` and registers the hook:

```sh
dotnet-gitmoji init --mode task-runner
```

> [!TIP]
> Add `--config` to any `init` call to also generate a `.gitmojirc.json` with defaults:
> ```sh
> dotnet-gitmoji init --mode shell --config
> ```

---

## Configuration

### Interactive wizard

The quickest way to configure preferences — walks through every option and saves to the global config:

```sh
dotnet-gitmoji config
```

### Manual configuration

Create a `.gitmojirc.json` in your repo root, or generate one with defaults:

```sh
dotnet-gitmoji init --config
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

| Command                           | Description                                         |
|-----------------------------------|-----------------------------------------------------|
| `dotnet-gitmoji init`             | Install the `prepare-commit-msg` hook               |
| `dotnet-gitmoji remove`           | Uninstall the hook                                  |
| `dotnet-gitmoji commit`           | Interactive commit (client mode)                    |
| `dotnet-gitmoji config`           | Run the configuration wizard                        |
| `dotnet-gitmoji list`             | List all available gitmojis                         |
| `dotnet-gitmoji search <keyword>` | Fuzzy-search gitmojis by name, code, or description |
| `dotnet-gitmoji update`           | Refresh the cached gitmoji list from the remote API |

---

## License

MIT — [Jonathan Búcaro](https://github.com/jebucaro)

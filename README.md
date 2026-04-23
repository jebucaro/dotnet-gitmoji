# dotnet-gitmoji

A [gitmoji](https://gitmoji.dev) commit convention CLI tool for .NET. It helps you write standardized, emoji-prefixed commit messages either interactively from the terminal or automatically via a Git hook.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later

## Installation

Install as a global .NET tool:

```sh
dotnet tool install --global dotnet-gitmoji
```

Or as a local tool in your project:

```sh
dotnet tool install dotnet-gitmoji
```

## Usage modes

dotnet-gitmoji supports two modes of operation:

### Hook mode (recommended)

The hook intercepts every `git commit` call and prompts you to pick a gitmoji before the commit is finalized. The commit message you type in your editor is pre-filled as the title suggestion.

Install the hook:

```sh
dotnet-gitmoji init
```

Then commit normally, the hook handles the rest:

```sh
git commit
```

To remove the hook:

```sh
dotnet-gitmoji remove
```

### Client mode

Use `dotnet-gitmoji commit` instead of `git commit`. This mode is not available when the hook is already installed (to avoid applying the emoji twice).

```sh
dotnet-gitmoji commit
dotnet-gitmoji commit --title "fix login redirect"
dotnet-gitmoji commit --title "fix login redirect" --scope auth --message "Resolves #42"
```

| Option | Short | Description |
|---|---|---|
| `--title` | `-t` | Commit title (skips the title prompt) |
| `--scope` | `-s` | Commit scope (skips the scope prompt) |
| `--message` | `-m` | Commit message body |

## Husky.Net integration

If your project already uses [Husky.Net](https://alirezanet.github.io/Husky.Net/), pass `--mode` to `init`:

```sh
# Shell mode — appends a command to .husky/prepare-commit-msg
dotnet-gitmoji init --mode shell

# Task-runner mode — adds a task to .husky/task-runner.json
dotnet-gitmoji init --mode task-runner
```

Use `--config` to also create a `.gitmojirc.json` in the repo root at the same time:

```sh
dotnet-gitmoji init --config
```

## Configuration

Configuration is loaded from the first `.gitmojirc.json` found, searching upward from the repository root. If none is found, the global config at `~/.dotnet-gitmoji/config.json` is used. If neither exists, built-in defaults apply.

### Interactive configuration wizard

```sh
dotnet-gitmoji config
```

This walks you through every option and saves the result to the global config file.

### Manual configuration

Create a `.gitmojirc.json` in your repository root (or run `dotnet-gitmoji init --config`):

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

| Key | Type | Default | Description |
|---|---|---|---|
| `emojiFormat` | `"Emoji"` \| `"Code"` | `"Emoji"` | Whether to prefix commits with the emoji character (`🐛`) or its shortcode (`:bug:`) |
| `scopePrompt` | `bool` | `false` | Prompt for a commit scope (e.g. `feat(auth): …`) |
| `messagePrompt` | `bool` | `false` | Prompt for an optional commit message body |
| `capitalizeTitle` | `bool` | `true` | Capitalize the first letter of the commit title automatically |
| `gitmojisUrl` | `string` | `https://gitmoji.dev/api/gitmojis` | HTTPS URL to fetch the gitmoji list from |
| `autoAdd` | `bool` | `false` | Stage all changes before committing *(client mode only)* |
| `signedCommit` | `bool` | `false` | Sign commits with GPG (`git commit -S`) *(client mode only)* |
| `scopes` | `string[]` \| `null` | `null` | Predefined scope suggestions shown when `scopePrompt` is `true` |

### Per-repository vs global config

| Location | Purpose |
|---|---|
| `.gitmojirc.json` in repo root (or any parent directory) | Shared team settings committed to the repository |
| `~/.dotnet-gitmoji/config.json` | Personal global fallback |

The repo-level file takes precedence over the global file.

## Other commands

| Command | Description |
|---|---|
| `dotnet-gitmoji list` | List all available gitmojis |
| `dotnet-gitmoji search <keyword>` | Fuzzy-search gitmojis by name, code, or description |
| `dotnet-gitmoji update` | Refresh the cached gitmoji list from the remote API |

## License

MIT

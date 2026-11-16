// Progressive enhancement: the page is fully usable without this file.

const COPIED_MS = 2000;
const COPY_KEYS = /Mac|iPhone|iPad/.test(navigator.userAgentData?.platform ?? navigator.platform) ? "⌘C" : "Ctrl+C";

let clearTimer;

// The live region is shared by every copy button, so it clears on its own timer, not a button's.
// Repeating an identical message makes some screen readers skip it, so vary it with a no-break space.
function announce(status, message) {
  status.textContent = status.textContent === message ? `${message}\u00a0` : message;
  clearTimeout(clearTimer);
  clearTimer = setTimeout(() => { status.textContent = ""; }, COPIED_MS);
}

function codeText(block) {
  const code = block.querySelector("code");
  const clone = code.cloneNode(true);
  clone.querySelectorAll(".prompt").forEach((p) => p.remove());
  return { code, text: clone.textContent };
}

function selectCode(code) {
  const range = document.createRange();
  range.selectNodeContents(code);
  const prompt = code.querySelector(".prompt");
  if (prompt) range.setStartAfter(prompt);
  const sel = window.getSelection();
  sel.removeAllRanges();
  sel.addRange(range);
}

function initCopy() {
  const status = document.createElement("p");
  status.className = "sr-only";
  status.setAttribute("aria-live", "polite");
  document.body.append(status);

  document.querySelectorAll(".code[data-copy]").forEach((block) => {
    const button = block.querySelector(".copy");
    if (!button) return;
    let timer;
    button.addEventListener("click", async () => {
      const { code, text } = codeText(block);
      let copied = false;
      try {
        if (!navigator.clipboard) throw new Error("Clipboard API unavailable");
        await navigator.clipboard.writeText(text);
        copied = true;
      } catch {
        selectCode(code);
      }
      button.textContent = copied ? "Copied" : `Press ${COPY_KEYS}`;
      button.classList.remove(copied ? "is-failed" : "is-copied");
      button.classList.add(copied ? "is-copied" : "is-failed");
      announce(status, copied ? "Copied to clipboard" : "Selected. Press Ctrl+C or Cmd+C to copy.");
      clearTimeout(timer);
      timer = setTimeout(() => {
        button.textContent = "Copy";
        button.classList.remove("is-copied", "is-failed");
      }, COPIED_MS);
    });
  });
}

function initVideo() {
  document.querySelectorAll("a.video[data-video-id]").forEach((link) => {
    link.addEventListener("click", (event) => {
      event.preventDefault();
      const iframe = document.createElement("iframe");
      iframe.src = `https://www.youtube-nocookie.com/embed/${link.dataset.videoId}?autoplay=1&rel=0`;
      iframe.title = "dotnet-gitmoji demo video";
      iframe.allow = "autoplay; encrypted-media; picture-in-picture";
      iframe.allowFullscreen = true;
      link.replaceWith(iframe);
      iframe.focus();
    });
  });
}

function initTargets() {
  document.querySelectorAll("details.targets").forEach((d) => d.removeAttribute("open"));
}

function initMenu() {
  const header = document.querySelector(".site-header");
  const button = header?.querySelector(".menu-toggle");
  if (!button) return;

  const setOpen = (open) => {
    header.classList.toggle("nav-open", open);
    button.setAttribute("aria-expanded", String(open));
  };

  header.classList.add("has-menu");
  button.hidden = false;
  button.addEventListener("click", () => setOpen(button.getAttribute("aria-expanded") !== "true"));
  header.querySelectorAll("nav a").forEach((a) => a.addEventListener("click", () => setOpen(false)));
  document.addEventListener("keydown", (event) => {
    if (event.key === "Escape" && header.classList.contains("nav-open")) {
      setOpen(false);
      button.focus();
    }
  });
}

initCopy();
initVideo();
initTargets();
initMenu();

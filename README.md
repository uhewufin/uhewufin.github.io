# LemonLoader Web

Browser tool for patching Quest games and managing LemonLoader mods over WebUSB.

## Layout
- `index.html` is the whole site.
- `agent/` is the C# program that runs on the headset (currently a test build).
- `.github/workflows/pages.yml` builds the agent and deploys the site to GitHub Pages.

## Setup
1. Push everything to a GitHub repo.
2. In the repo, open Settings, then Pages, and set Source to GitHub Actions.
3. Push to `main`. The workflow builds the agent and publishes the site.

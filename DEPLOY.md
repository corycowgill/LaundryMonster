# Deploying Laundry Monster

The repo has two branches with different jobs:

| Branch | Contains | Why |
|---|---|---|
| `main` | source, assets, generated models and textures | the project. `Builds/` is gitignored here. |
| `deploy` | **only** the built WebGL game | what Render serves. Generated output, never edited by hand. |

Keeping them apart means the source history is not polluted with an 18 MB binary
blob on every build, and Render clones only what it needs to serve.

## First-time Render setup

Two ways. The blueprint is the easy one.

### A. Blueprint (one click)

`render.yaml` is published onto the `deploy` branch with every build, so Render can
configure the site itself:

1. Render dashboard -> **New** -> **Blueprint**
2. Pick the `corycowgill/LaundryMonster` repo
3. Set the branch to **`deploy`**
4. Apply

### B. By hand

Create a **Static Site** (not a Web Service - there is nothing to run server side):

| Field | Value |
|---|---|
| Repository | `corycowgill/LaundryMonster` |
| Branch | **`deploy`** |
| Root Directory | *(leave blank)* |
| Build Command | *(leave blank)* |
| Publish Directory | **`.`** |

That is the whole configuration. There is no build step, because Unity builds the game
locally and the output is committed to `deploy` already.

### Why no custom headers are needed

Unity ships its payload pre-compressed. Normally the host must send
`Content-Encoding: gzip` or the loader fails with a misleading *"Incorrect response
MIME type. Expected 'application/wasm'"*. Render does not set that header for
pre-compressed files by default.

Rather than depend on host configuration, the build sets
`PlayerSettings.WebGL.decompressionFallback = true`, so Unity decompresses in
JavaScript. It costs a little startup time and works on any static host.

If you later want the faster path, set these headers in Render and turn the fallback
off in `Assets/Editor/Builder.cs`:

```
/Build/*.unityweb   Content-Encoding: gzip
```

## Publishing a new build

Pick whichever is in front of you. All three do the same thing.

**From Unity (easiest):**

> menu **Laundry Monster > Build and Deploy WebGL**

Builds the player and pushes it to `deploy` in one click, after a confirmation prompt.
**Laundry Monster > Deploy Last Build** publishes the existing build without rebuilding.

**From PowerShell:**

```powershell
.\deploy.ps1
```

**From a shell:**

```bash
./publish-deploy.sh
```

Render redeploys automatically on every push to `deploy`, so there is nothing to click
afterwards. The script is safe to re-run: it works in a throwaway git worktree, never
touches your working tree, and stops early if the build is already published.

Build first if you are using the script directly - Unity menu **Laundry Monster > Build
WebGL** - since the scripts publish whatever is in `Builds/WebGL` and do not build.

## What the player loads

1. `index.html` from the custom template in `Assets/WebGLTemplates/Hallucinated`
2. `TemplateData/hallucinated-intro.js` - the studio ident, which starts
   **in parallel** with the download so it covers load time instead of adding to it
3. The Unity build itself

Add `?nointro` to the URL to skip the ident, or `?debug` which also skips it.

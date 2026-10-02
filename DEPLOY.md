# Deploying Laundry Monster

The repo has two branches with different jobs:

| Branch | Contains | Why |
|---|---|---|
| `main` | source, assets, generated models and textures | the project. `Builds/` is gitignored here. |
| `deploy` | **only** the built WebGL game | what Render serves. Generated output, never edited by hand. |

Keeping them apart means the source history is not polluted with an 18 MB binary
blob on every build, and Render clones only what it needs to serve.

## Render.com settings

Create a **Static Site** (not a Web Service - there is nothing to run server side):

| Field | Value |
|---|---|
| Repository | `corycowgill/LaundryMonster` |
| Branch | **`deploy`** |
| Root Directory | *(leave blank)* |
| Build Command | *(leave blank)* |
| Publish Directory | **`.`** |

That is the whole configuration. There is no build step, because the game is built
locally by Unity and committed to `deploy` already.

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

```bash
# 1. Build WebGL from the Unity Editor:
#    menu  Laundry Monster > Build WebGL
#    (output lands in Builds/WebGL, which is gitignored on main)

# 2. Push that output to the deploy branch:
./publish-deploy.sh
```

Render auto-deploys on every push to `deploy`.

## What the player loads

1. `index.html` from the custom template in `Assets/WebGLTemplates/Hallucinated`
2. `TemplateData/hallucinated-intro.js` - the studio ident, which starts
   **in parallel** with the download so it covers load time instead of adding to it
3. The Unity build itself

Add `?nointro` to the URL to skip the ident, or `?debug` which also skips it.

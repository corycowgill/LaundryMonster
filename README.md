# Laundry Monster - deployed build

This branch contains **only the built WebGL game**. It is generated output, not source.
Source lives on `main`.

Render.com settings:
  Type               Static Site
  Branch             deploy
  Root Directory     (leave blank)
  Build Command      (leave blank)
  Publish Directory  .

The build has `decompressionFallback` enabled, so it decompresses in JavaScript and
does not need the host to send `Content-Encoding: gzip`.

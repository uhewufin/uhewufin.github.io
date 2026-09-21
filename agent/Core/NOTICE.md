# Origin of the code in this folder

Most of this folder is taken from the LemonLoader installer's `Core` library:
https://github.com/LemonLoader/MelonLoaderInstaller

That project is licensed under the GNU GPL v3 (see `LICENSE` in this folder), so the
agent program built from it is also GPL-3.0 and its source must stay available.

Parts of `Utilities/Axml` and `Utilities/Signing` come from QuestPatcher (zlib license):
https://github.com/Lauriethefish/QuestPatcher

Changes made for this project:
- Removed the plugin loader and the downloader (the website supplies the files instead).
- `Patcher` prints `@step` progress lines for the website.
- `UnityVersionDetector` can run on its own.
- SHA-256 uses BouncyCastle (`Sha256Compat`) so no OS crypto library is needed.
- The certificate step falls back to the built-in certificate if generation throws.

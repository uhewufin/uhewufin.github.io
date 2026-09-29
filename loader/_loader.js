// _loader.js — the one script every patched game's gadget runs on load.
// Works out its own package name from /proc/self/cmdline, then loads every
// enabled .js mod it finds in that game's mods folder.

const readFile = new NativeFunction(Module.getExportByName(null, 'open'), 'int', ['pointer', 'int']);
const readCall = new NativeFunction(Module.getExportByName(null, 'read'), 'long', ['int', 'pointer', 'long']);
const closeCall = new NativeFunction(Module.getExportByName(null, 'close'), 'int', ['int']);

function readWholeFile(path) {
  const fd = readFile(Memory.allocUtf8String(path), 0 /* O_RDONLY */);
  if (fd < 0) return null;
  const buf = Memory.alloc(4096);
  const n = readCall(fd, buf, 4096);
  closeCall(fd);
  if (n <= 0) return null;
  return buf.readCString(n);
}

const pkg = (readWholeFile('/proc/self/cmdline') || '').split('\0')[0];
if (!pkg) {
  console.log('[loader] could not determine package name from /proc/self/cmdline');
} else {
  const modsDir = `/sdcard/Android/data/${pkg}/files/mods/`;

  const opendir = new NativeFunction(Module.getExportByName(null, 'opendir'), 'pointer', ['pointer']);
  const readdir = new NativeFunction(Module.getExportByName(null, 'readdir'), 'pointer', ['pointer']);
  const closedir = new NativeFunction(Module.getExportByName(null, 'closedir'), 'int', ['pointer']);

  const dp = opendir(Memory.allocUtf8String(modsDir));
  if (dp.isNull()) {
    console.log('[loader] mods folder not found: ' + modsDir);
  } else {
    let entry;
    while (!(entry = readdir(dp)).isNull()) {
      // d_name offset on bionic's struct dirent (arm64) — unverified on a real device.
      const name = entry.add(19).readUtf8String();
      if (name && name.endsWith('.js') && !name.endsWith('.disabled.js') && name !== '_loader.js') {
        try {
          const code = new File(modsDir + name, 'r').readText();
          (1, eval)(code);
          console.log('[loader] loaded ' + name);
        } catch (e) {
          console.log('[loader] failed to load ' + name + ': ' + e);
        }
      }
    }
    closedir(dp);
  }
}

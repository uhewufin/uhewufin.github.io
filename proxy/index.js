export default {
  async fetch(request) {
    const url = new URL(request.url);
    const version = url.searchParams.get("version") || "";
    const region = url.searchParams.get("region") || "global";

    // Matches "2019.4.26" and "2021.3.45f2" style versions, nothing else.
    if (!/^\d{4}\.\d+\.\d+([a-z]\d+)?$/i.test(version)) {
      return new Response("Invalid Unity version format.", { status: 400 });
    }

    const repos = {
      global: "LavaGang/MelonLoader.UnityDependencies",
      china: "LemonLoader/MelonLoader.UnityDependencies.China",
    };
    const repo = repos[region];
    if (!repo) {
      return new Response("Invalid region. Use 'global' or 'china'.", { status: 400 });
    }

    const target = `https://github.com/${repo}/releases/download/${version}/libunity.so.arm64-v8a`;

    const upstream = await fetch(target, { redirect: "follow" });
    if (!upstream.ok) {
      return new Response(`No libunity.so found for ${version} (${region}), HTTP ${upstream.status}.`, {
        status: upstream.status,
      });
    }

    const headers = new Headers(upstream.headers);
    headers.set("Access-Control-Allow-Origin", "*");
    headers.set("Cache-Control", "public, max-age=86400");

    return new Response(upstream.body, { status: 200, headers });
  },
};

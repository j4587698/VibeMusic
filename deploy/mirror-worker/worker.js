/**
 * VibeMusic 更新分发中转（Cloudflare Worker）。
 *
 * 客户端在国内访问不了 GitHub，但能访问 Cloudflare 边缘节点。
 * 这个 Worker 把固定形状的更新请求回源到 GitHub Release，并在边缘缓存，
 * 不需要服务器、不需要备案、不需要提前上传产物。
 *
 * 路由（其余一律 404，见下方「为什么必须白名单」）：
 *   GET /latest.json            -> releases/latest/download/latest.json
 *   GET /latest.json.sig        -> releases/latest/download/latest.json.sig
 *   GET /v1.2.0/<asset>         -> releases/download/v1.2.0/<asset>
 *
 * 这套路径与 CI 生成清单时拼的 `{UPDATE_MIRROR_BASE_URL}/{tag}/{fileName}`
 * 以及客户端拉清单用的 `{MirrorBaseUrl}/latest.json` 完全对应，
 * 所以启用中转时只需设 UPDATE_MIRROR_BASE_URL，无需改动 CI 的上传步骤。
 */

/** 清单变化频繁且体积极小，缓存短一点，避免用户等太久才看到新版本。 */
const MANIFEST_TTL = 300;

/** 带版本号的产物内容永不改变，可以长期缓存。 */
const ASSET_TTL = 31536000;

const MANIFEST_FILES = new Set(["latest.json", "latest.json.sig"]);

/** 形如 v1.2.0 或 v1.2.0-beta.1 */
const TAG_PATTERN = /^v\d+\.\d+\.\d+(?:-[0-9A-Za-z.]+)?$/;

/** 形如 vibemusic-1.2.0-win-x64.zip / vibemusic-1.2.0-arm64-v8a.apk */
const ASSET_PATTERN =
  /^vibemusic-\d+\.\d+\.\d+(?:-[0-9A-Za-z.]+)?-[a-z0-9][a-z0-9-]*\.(?:zip|apk)$/;

export default {
  /**
   * @param {Request} request
   * @param {{ GITHUB_REPOSITORY: string }} env
   * @param {ExecutionContext} ctx
   */
  async fetch(request, env, ctx) {
    if (request.method !== "GET" && request.method !== "HEAD") {
      return new Response("Method Not Allowed", {
        status: 405,
        headers: { Allow: "GET, HEAD" },
      });
    }

    const repo = env.GITHUB_REPOSITORY;
    if (!repo || !/^[\w.-]+\/[\w.-]+$/.test(repo)) {
      return new Response("Worker misconfigured: GITHUB_REPOSITORY", { status: 500 });
    }

    const url = new URL(request.url);
    const target = resolveUpstream(repo, url.pathname);
    if (!target) {
      return new Response("Not Found", { status: 404 });
    }

    // 带 Range 的请求（断点续传）直接透传，不进边缘缓存：
    // 缓存 206 部分响应意义不大，还容易和完整响应互相污染。
    const range = request.headers.get("Range");
    if (range) {
      return fetchUpstream(target.url, { Range: range }, target.ttl, false);
    }

    const cache = caches.default;
    const cacheKey = new Request(url.toString(), { method: "GET" });

    const cached = await cache.match(cacheKey);
    if (cached) {
      const hit = new Response(cached.body, cached);
      hit.headers.set("X-Mirror-Cache", "HIT");
      return hit;
    }

    const response = await fetchUpstream(target.url, {}, target.ttl, true);
    if (response.status === 200) {
      // 克隆一份写缓存，不阻塞返回给客户端。
      ctx.waitUntil(cache.put(cacheKey, response.clone()));
    }

    response.headers.set("X-Mirror-Cache", "MISS");
    return response;
  },
};

/**
 * 把请求路径映射到 GitHub Release 地址。
 *
 * 为什么必须白名单：如果允许任意路径透传，这个 Worker 就成了公开的匿名代理，
 * 会被拿去刷流量、绕墙、甚至攻击第三方，账号很快被封。只放行已知形状的路径。
 *
 * @param {string} repo
 * @param {string} pathname
 * @returns {{ url: string, ttl: number } | null}
 */
function resolveUpstream(repo, pathname) {
  const segments = pathname.split("/").filter((s) => s.length > 0);

  if (segments.length === 1 && MANIFEST_FILES.has(segments[0])) {
    // releases/latest/download/<name> 是 GitHub 的稳定重定向，
    // 永远指向最新 release 的同名资产，不用调 API、没有速率限制。
    return {
      url: `https://github.com/${repo}/releases/latest/download/${segments[0]}`,
      ttl: MANIFEST_TTL,
    };
  }

  if (segments.length === 2) {
    const [tag, name] = segments;
    if (!TAG_PATTERN.test(tag)) {
      return null;
    }

    if (MANIFEST_FILES.has(name)) {
      return {
        url: `https://github.com/${repo}/releases/download/${tag}/${name}`,
        ttl: MANIFEST_TTL,
      };
    }

    if (ASSET_PATTERN.test(name)) {
      return {
        url: `https://github.com/${repo}/releases/download/${tag}/${name}`,
        ttl: ASSET_TTL,
      };
    }
  }

  return null;
}

/**
 * @param {string} upstream
 * @param {Record<string, string>} extraHeaders
 * @param {number} ttl
 * @param {boolean} allowEdgeCache
 */
async function fetchUpstream(upstream, extraHeaders, ttl, allowEdgeCache) {
  const upstreamResponse = await fetch(upstream, {
    headers: { "User-Agent": "vibemusic-update-mirror", ...extraHeaders },
    redirect: "follow",
    cf: allowEdgeCache ? { cacheEverything: true, cacheTtl: ttl } : undefined,
  });

  const headers = new Headers(upstreamResponse.headers);
  headers.set(
    "Cache-Control",
    ttl === MANIFEST_TTL ? "no-cache" : `public, max-age=${ttl}, immutable`,
  );
  // 客户端要靠 Content-Length 显示进度，靠 Accept-Ranges 续传。
  headers.set("Access-Control-Allow-Origin", "*");
  headers.delete("Set-Cookie");

  return new Response(upstreamResponse.body, {
    status: upstreamResponse.status,
    statusText: upstreamResponse.statusText,
    headers,
  });
}

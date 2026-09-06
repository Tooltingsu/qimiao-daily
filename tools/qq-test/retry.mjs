// SDK 1.0.4 exposes only ApiError status/code, not HTTP headers.  This helper
// honours Retry-After immediately if a future SDK/transport exposes it, while
// retaining a small bounded backoff for the current official facade.
export function retryDelayMs(error, attempt) {
  const header = error?.headers?.get?.("retry-after") ?? error?.retryAfter;
  const seconds = Number(header);
  if (Number.isFinite(seconds) && seconds >= 0) return Math.min(60_000, Math.round(seconds * 1000));
  const milliseconds = Number(error?.retryAfterMs);
  if (Number.isFinite(milliseconds) && milliseconds >= 0) return Math.min(60_000, Math.round(milliseconds));
  return Math.min(10_000, 1000 * Math.max(1, attempt));
}

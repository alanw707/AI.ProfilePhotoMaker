/**
 * Where the pricing page sends a buyer who came from the photo workspace.
 *
 * Only `/app/enhance` returns are honoured; every original query param is kept (so the
 * workspace's preview id and the career back link survive checkout, #391) and the
 * purchased package is added as `upgraded`.
 */
export function workspaceReturnAfterPurchase(
  returnUrl: string | null,
  outcomePackage: string | null
): { path: string; queryParams: Record<string, string> } | null {
  if (!returnUrl?.startsWith('/app/enhance') || !outcomePackage) {
    return null;
  }
  const [path, query = ''] = returnUrl.split('?');
  const params = new URLSearchParams(query);
  params.set('upgraded', outcomePackage);
  return { path, queryParams: Object.fromEntries(params.entries()) };
}

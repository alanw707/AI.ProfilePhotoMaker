# Purchase delivers the raw preview asset, server-side

Status: accepted (supersedes ADR-0003)

ADR-0003 made preview promotion a client-driven option: the browser had to still hold the preview candidate, style, and source path in `sessionStorage`, then send `ReusedPreviewProcessedImageId` and click a separate "confirm paid generation" step. Any lost session, new device, or reload turned a paid Starter into "locked until purchase grants an entitlement", and a customer who paid twice still could not obtain the unwatermarked image she had already seen and asked to refund.

We are therefore making the purchase itself the delivery event: on a successful Starter or Pro purchase the server creates the promoted candidate from the existing raw preview asset, with no provider call and no client participation, and the workspace reads the active package from the server on every entrance rather than from browser state. Because the promoted candidate consumes one candidate slot, the pre-checkout copy must state the outcome plainly ("your preview unwatermarked + 2 more shots"), and the upgrade CTA must be withheld once the raw preview asset has expired, since we cannot sell a photo that no longer exists.

Consequences: raw preview assets leave only through an authorized, entitlement-checked endpoint, and the unauthenticated storage proxy must refuse the private path outright; the raw path moves out of `ProcessedImage.FailureReason` into a real column; a style change after purchase keeps the promoted candidate and generates the remaining candidates in the new style.

# ImageSharp: licence or replace (issue #439) — decision brief, 2026-10-11

**Status: needs an owner decision.** Nothing was changed in the code. The choice involves accepting
licence terms or paying, which an agent must not do on the owner's behalf.

## Where things stand

- Both projects pin `SixLabors.ImageSharp` **3.1.11** and suppress five advisories with
  `NuGetAuditSuppress` (PR #438) so CI passes.
- All five advisories are fixed only in **4.1.2** (GitHub advisory database, checked 2026-10-11).
  The repo note says 4.x needs a Six Labors licence at build time. I did not re-verify the current
  licence terms or price; check sixlabors.com before deciding.

| Advisory | Severity | What it is | Reachable in this app? |
|---|---|---|---|
| GHSA-j3p4-wp97-rph4 | high | `HistogramEqualization` unchecked index | No: the processor is not used anywhere |
| GHSA-j9gm-c75j-xc9q | high | TIFF CCITT T4 **encoder** overflow | No: the app never writes TIFF |
| GHSA-jjfr-hcj7-qf5w | high | TIFF CCITT T6 **encoder** overflow | No: the app never writes TIFF |
| GHSA-wmxv-xphr-5c9g | medium | BigTIFF decoder can loop without progress | **Was reachable** by any signed-in user through the photo score upload, which checked size only. Closed for that path on 2026-10-12 (see the correction below); six other load sites still use the default decoders |
| GHSA-gwg2-r3hj-4w44 | medium | ICC profile parsing allocates from unvalidated sizes | Possibly: depends on whether a profile is parsed on load; not confirmed either way |

So the three "high" items are not reachable from this code. The two "medium" items are
denial-of-service risks on image decode and may be reachable with a crafted upload.
`ImageController` checks file signatures for JPEG/PNG/WebP on one upload path; I did not confirm
that every path into the seven load sites is covered.

## How much of the app uses it

Six API source files (plus tests): load, auto-orient, resize, composite (`DrawImage`), JPEG/PNG save, and per-pixel reads
(`Rgba32`) for the photo score. Main sites: `OpenAIImageGenerationService`, `HeadshotGenerationService`,
`PlatformExportService`, `ProfilePhotoScoreService`, `UploadStylePreviewsService`, `CareerExportService`.
These sit on the paid photo path, so any change needs the full photo test suites and a visual check.

## Options

| Option | Cost | Risk | Removes the suppressions? |
|---|---|---|---|
| **A. Get a licence and upgrade to 4.1.2+** | Owner time to apply or pay; small code change for 4.x API differences | Low | Yes |
| **B. Migrate to an MIT/Apache library** (SkiaSharp or Magick.NET) | Largest: nine files, native libraries in the container image, pixel-level output differences to re-verify | Medium (touches paid output) | Yes |
| **C. Stay on 3.1.11, restrict decoders to JPEG/PNG/WebP** | Small: one shared `DecoderOptions` used at the seven load sites, plus tests | Low | No, but closes the BigTIFF path and narrows the rest |

## Recommendation

1. Do **C now** as a stop-gap. It is small, testable and removes the clearest reachable issue.
2. Decide between **A** and **B** on licence terms: if the project qualifies for a free or
   acceptable Six Labors licence, take **A**; if not, schedule **B** with SkiaSharp.
3. Do not leave the suppressions in place indefinitely. They also hide any future advisory with
   the same ids.

## What is needed from the owner

One answer: is a Six Labors licence acceptable (free tier if eligible, or paid)? Yes → A. No → B.

## Correction and partial fix, 2026-10-12

An independent review found this brief understated the risk: `POST /api/ProfilePhotoWorkflow/score`
checked only file size, so a signed-in user could send a TIFF straight to the default decoders.

Fixed for that path: the endpoint now rejects anything that does not start like a JPEG, PNG or WebP
(`UnsupportedImageFormat`), and `ProfilePhotoScoreService` decodes with JPEG/PNG/WebP decoders only
(`UploadedImageFormats`). This is option C applied to one of the seven load sites.

Still open: the other six load sites use the default decoders. Their inputs are stored uploads that
passed `ImageController.IsValidImageFile`, model output, or local files, so they are lower risk, but
that check accepts any `RIFF` file named `.webp`. Moving them to `UploadedImageFormats.DecoderOptions`
is the rest of option C. The licence question (A or B) is unchanged.


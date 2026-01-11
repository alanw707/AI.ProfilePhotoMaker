#!/usr/bin/env python3
"""Post a LinkedIn UGC update (text-only, link, or image) via LinkedIn Marketing API.

Requires:
  - LINKEDIN_ACCESS_TOKEN (OAuth token)
  - LINKEDIN_AUTHOR_URN (urn:li:person:{id} or urn:li:organization:{id})

Optional env:
  - LINKEDIN_VISIBILITY (PUBLIC|CONNECTIONS, default PUBLIC)

Examples:
  LINKEDIN_ACCESS_TOKEN=... LINKEDIN_AUTHOR_URN=urn:li:organization:123 \
    python3 scripts/linkedin/autopost_ugc.py --text "Hello" \
    --link "https://example.com"

  LINKEDIN_ACCESS_TOKEN=... LINKEDIN_AUTHOR_URN=urn:li:organization:123 \
    python3 scripts/linkedin/autopost_ugc.py --text "Check this" \
    --image-asset-urn "urn:li:digitalmediaAsset:..."
"""

import argparse
import json
import os
import sys
import textwrap
from typing import Any, Dict

import requests

API_URL = "https://api.linkedin.com/v2/ugcPosts"


def build_payload(
    author_urn: str,
    text: str,
    visibility: str,
    link: str | None,
    image_asset_urn: str | None,
) -> Dict[str, Any]:
    share_media_category = "NONE"
    media: list[Dict[str, Any]] = []

    if link and image_asset_urn:
        raise ValueError("Choose either --link or --image-asset-urn, not both.")

    if link:
        share_media_category = "ARTICLE"
        media = [
            {
                "status": "READY",
                "description": {"text": ""},
                "originalUrl": link,
                "title": {"text": ""},
            }
        ]
    elif image_asset_urn:
        share_media_category = "IMAGE"
        media = [
            {
                "status": "READY",
                "description": {"text": ""},
                "media": image_asset_urn,
                "title": {"text": ""},
            }
        ]

    payload: Dict[str, Any] = {
        "author": author_urn,
        "lifecycleState": "PUBLISHED",
        "specificContent": {
            "com.linkedin.ugc.ShareContent": {
                "shareCommentary": {"text": text},
                "shareMediaCategory": share_media_category,
            }
        },
        "visibility": {
            "com.linkedin.ugc.MemberNetworkVisibility": visibility,
        },
    }

    if media:
        payload["specificContent"]["com.linkedin.ugc.ShareContent"]["media"] = media

    return payload


def main() -> int:
    parser = argparse.ArgumentParser(
        formatter_class=argparse.RawDescriptionHelpFormatter,
        description="Post a LinkedIn UGC update via API.",
        epilog=textwrap.dedent(
            """
            Notes:
              - Token must include appropriate scopes (e.g., w_member_social or w_organization_social).
              - For image posts, upload the asset first and pass the asset URN.
            """
        ),
    )
    parser.add_argument("--text", required=True, help="Post text/commentary")
    parser.add_argument("--link", help="Optional link URL (ARTICLE share)")
    parser.add_argument("--image-asset-urn", help="Optional image asset URN")
    args = parser.parse_args()

    access_token = os.getenv("LINKEDIN_ACCESS_TOKEN")
    author_urn = os.getenv("LINKEDIN_AUTHOR_URN")
    visibility = os.getenv("LINKEDIN_VISIBILITY", "PUBLIC")

    if not access_token or not author_urn:
        print("Missing LINKEDIN_ACCESS_TOKEN or LINKEDIN_AUTHOR_URN in env.", file=sys.stderr)
        return 2

    try:
        payload = build_payload(
            author_urn=author_urn,
            text=args.text,
            visibility=visibility,
            link=args.link,
            image_asset_urn=args.image_asset_urn,
        )
    except ValueError as exc:
        print(str(exc), file=sys.stderr)
        return 2

    headers = {
        "Authorization": f"Bearer {access_token}",
        "Content-Type": "application/json",
        "X-Restli-Protocol-Version": "2.0.0",
    }

    response = requests.post(API_URL, headers=headers, data=json.dumps(payload), timeout=30)

    if response.status_code >= 300:
        print("LinkedIn API error:", response.status_code, response.text, file=sys.stderr)
        return 1

    print(response.text)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

#!/usr/bin/env python3
"""Upload an Android App Bundle to a Google Play track through the Play Developer API.

Needs a service account that has release rights for the app in Play Console
(Users and permissions). Its JSON key is passed with --key or PLAY_SERVICE_ACCOUNT_JSON
and must never be committed.

Examples:
  # upload as a draft release on the internal track (nothing reaches testers yet)
  python tools/play_upload.py --aab build/too-fishy.aab --notes "Lava fix"
  # roll out to internal testers right away
  python tools/play_upload.py --aab build/too-fishy.aab --status completed
  # only show the current releases of a track
  python tools/play_upload.py --list

Requires: pip install google-api-python-client google-auth
"""
import argparse
import os
import sys

from google.oauth2 import service_account
from googleapiclient.discovery import build
from googleapiclient.http import MediaFileUpload

SCOPES = ["https://www.googleapis.com/auth/androidpublisher"]


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--package", default="de.zackig.toofishy")
    ap.add_argument("--aab", help="path to the .aab to upload")
    ap.add_argument("--track", default="internal", help="internal, alpha, beta or production")
    ap.add_argument("--status", default="draft", choices=["draft", "completed"],
                    help="draft: create the release without rolling it out (default); completed: roll out")
    ap.add_argument("--release-name", help="defaults to the version name and code")
    ap.add_argument("--notes", help="release notes (en-US)")
    ap.add_argument("--notes-lang", default="en-US")
    ap.add_argument("--key", default=os.environ.get("PLAY_SERVICE_ACCOUNT_JSON"),
                    help="service account JSON key (or PLAY_SERVICE_ACCOUNT_JSON)")
    ap.add_argument("--list", action="store_true", help="only print the track's current releases")
    args = ap.parse_args()

    if not args.key or not os.path.isfile(args.key):
        ap.error("service account key not found: pass --key or set PLAY_SERVICE_ACCOUNT_JSON")
    if not args.list and (not args.aab or not os.path.isfile(args.aab)):
        ap.error("--aab must point to an existing .aab file")

    creds = service_account.Credentials.from_service_account_file(args.key, scopes=SCOPES)
    edits = build("androidpublisher", "v3", credentials=creds, cache_discovery=False).edits()
    edit_id = edits.insert(packageName=args.package, body={}).execute()["id"]

    try:
        if args.list:
            track = edits.tracks().get(packageName=args.package, editId=edit_id, track=args.track).execute()
            for r in track.get("releases", []):
                print(f"{r.get('name', '?')}: status={r.get('status')} versionCodes={r.get('versionCodes')}")
            edits.delete(packageName=args.package, editId=edit_id).execute()
            return 0

        print(f"Uploading {args.aab} ({os.path.getsize(args.aab) / 1e6:.1f} MB) ...", flush=True)
        media = MediaFileUpload(args.aab, mimetype="application/octet-stream", resumable=True,
                                chunksize=16 * 1024 * 1024)
        bundle = edits.bundles().upload(packageName=args.package, editId=edit_id, media_body=media).execute()
        code = bundle["versionCode"]
        print(f"Uploaded versionCode {code} (sha256 {bundle.get('sha256', '?')})")

        release = {"versionCodes": [str(code)], "status": args.status,
                   "name": args.release_name or f"{code}"}
        if args.notes:
            release["releaseNotes"] = [{"language": args.notes_lang, "text": args.notes}]
        # A draft goes next to the release testers have now (older drafts are replaced);
        # a completed release replaces what is on the track.
        releases = [release]
        if args.status == "draft":
            current = edits.tracks().get(packageName=args.package, editId=edit_id, track=args.track).execute()
            releases = [r for r in current.get("releases", []) if r.get("status") != "draft"] + [release]
        edits.tracks().update(packageName=args.package, editId=edit_id, track=args.track,
                              body={"track": args.track, "releases": releases}).execute()
        result = edits.commit(packageName=args.package, editId=edit_id).execute()
        print(f"Committed edit {result['id']}: track {args.track}, versionCode {code}, status {args.status}")
        return 0
    except Exception:
        try:
            edits.delete(packageName=args.package, editId=edit_id).execute()
        except Exception:
            pass
        raise


if __name__ == "__main__":
    sys.exit(main())

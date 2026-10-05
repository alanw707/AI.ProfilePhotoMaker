#!/usr/bin/env python3
"""Build the career occupation reference snapshot from an O*NET text database release.

Usage:
  curl -sSLo /tmp/onet.zip https://www.onetcenter.org/dl_files/database/db_30_0_text.zip
  python3 scripts/career/build-onet-snapshot.py /tmp/onet.zip

Writes AI.ProfilePhotoMaker.API/Data/Reference/onet-snapshot.json.gz (deterministic:
sorted keys, fixed gzip mtime) and prints its SHA-256. ADR 0010 explains the fields.
O*NET content is licensed CC BY 4.0; the attribution travels inside the snapshot.
"""
import csv
import gzip
import hashlib
import io
import json
import os
import sys
import zipfile
from collections import defaultdict

RELEASE = "30.0"
RELEASE_DATE = "2025-08"
SOURCE_URL = "https://www.onetcenter.org/dl_files/database/db_30_0_text.zip"
MAX_TASKS = 12
MAX_SKILLS = 10
MAX_TECH = 12
MAX_TITLES = 25
OUT = os.path.join(os.path.dirname(__file__), "..", "..", "AI.ProfilePhotoMaker.API", "Data", "Reference", "onet-snapshot.json.gz")


def rows(zf, name):
    member = next(n for n in zf.namelist() if n.endswith("/" + name))
    with zf.open(member) as raw:
        reader = csv.DictReader(io.TextIOWrapper(raw, encoding="utf-8"), delimiter="\t", quoting=csv.QUOTE_NONE)
        yield from reader


def main(path):
    with open(path, "rb") as f:
        source_sha = hashlib.sha256(f.read()).hexdigest()
    zf = zipfile.ZipFile(path)

    occupations = {r["O*NET-SOC Code"]: {"code": r["O*NET-SOC Code"], "title": r["Title"], "description": r["Description"]}
                   for r in rows(zf, "Occupation Data.txt")}

    tasks = defaultdict(list)
    for r in rows(zf, "Task Statements.txt"):
        if r["Task Type"] == "Core":
            tasks[r["O*NET-SOC Code"]].append((int(r["Task ID"]), r["Task"]))

    skills = defaultdict(list)
    for r in rows(zf, "Skills.txt"):
        if r["Scale ID"] == "IM" and r["Recommend Suppress"] != "Y":
            skills[r["O*NET-SOC Code"]].append((-float(r["Data Value"]), r["Element Name"]))

    tech = defaultdict(list)
    for r in rows(zf, "Technology Skills.txt"):
        tech[r["O*NET-SOC Code"]].append((0 if r["Hot Technology"] == "Y" else 1, r["Example"]))

    titles = defaultdict(set)
    for r in rows(zf, "Sample of Reported Titles.txt"):
        titles[r["O*NET-SOC Code"]].add(r["Reported Job Title"])
    for r in rows(zf, "Alternate Titles.txt"):
        titles[r["O*NET-SOC Code"]].add(r["Alternate Title"])

    out = []
    for code in sorted(occupations):
        if not tasks[code]:
            continue  # No core tasks means no duty evidence to match against.
        occ = occupations[code]
        occ["family"] = code[:2]
        occ["tasks"] = [{"id": i, "text": t} for i, t in sorted(tasks[code])[:MAX_TASKS]]
        occ["skills"] = [name for value, name in sorted(skills[code]) if -value >= 3.0][:MAX_SKILLS]
        seen, techs = set(), []
        for _, example in sorted(tech[code]):
            if example not in seen:
                seen.add(example)
                techs.append(example)
        occ["technologies"] = techs[:MAX_TECH]
        occ["titles"] = sorted(titles[code])[:MAX_TITLES]
        out.append(occ)

    snapshot = {
        "source": {
            "name": "O*NET 30.0 Database",
            "publisher": "U.S. Department of Labor, Employment and Training Administration",
            "release": RELEASE,
            "releaseDate": RELEASE_DATE,
            "taxonomy": "O*NET-SOC 2019",
            "url": SOURCE_URL,
            "sourceSha256": source_sha,
            "license": "CC BY 4.0",
            "licenseUrl": "https://creativecommons.org/licenses/by/4.0/",
            "attribution": (
                "This product includes information from the O*NET 30.0 Database by the U.S. Department of Labor, "
                "Employment and Training Administration (USDOL/ETA). Used under the CC BY 4.0 license. O*NET\u00ae is a "
                "trademark of USDOL/ETA. This product has been modified (selected core tasks, skills, technologies and "
                "titles) and is not endorsed by USDOL/ETA."
            ),
            "selection": {"maxTasks": MAX_TASKS, "maxSkills": MAX_SKILLS, "skillImportanceAtLeast": 3.0,
                          "maxTechnologies": MAX_TECH, "maxTitles": MAX_TITLES, "taskType": "Core"},
        },
        "occupations": out,
    }

    payload = json.dumps(snapshot, sort_keys=True, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "wb") as f:
        with gzip.GzipFile(fileobj=f, mode="wb", mtime=0, filename="") as gz:
            gz.write(payload)
    with open(OUT, "rb") as f:
        print(f"{len(out)} occupations; snapshot sha256 {hashlib.sha256(f.read()).hexdigest()}; source sha256 {source_sha}")


if __name__ == "__main__":
    main(sys.argv[1])

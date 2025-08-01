#!/bin/bash
ng lint 2>&1 | grep -E "error|warning" | sed 's/.*  //' | cut -d' ' -f2- | sort | uniq -c | sort -nr
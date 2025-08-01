#!/bin/bash
ng lint 2>&1 | grep -E "\.ts:|\.html:" | cut -d':' -f1 | sort | uniq -c | sort -nr | head -20
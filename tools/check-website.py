"""Check local website resources and the advertised Windows download digest."""
import hashlib
import re
from html.parser import HTMLParser
from pathlib import Path
from urllib.parse import urlsplit

root = Path(__file__).resolve().parents[1] / "website"

class Resources(HTMLParser):
    def handle_starttag(self, tag, attrs):
        for key, value in attrs:
            if key not in ("href", "src", "poster") or not value:
                continue
            url = urlsplit(value)
            if url.scheme or url.netloc or not url.path:
                continue
            assert (root / url.path).is_file(), f"Missing website resource: {value}"

html = (root / "index.html").read_text(encoding="utf-8")
Resources().feed(html)
digest = hashlib.sha256((root / "downloads/NotchPrompter.exe").read_bytes()).hexdigest()
assert digest in html, "Windows checksum in website does not match its download"
assert "data-mac-download" in html, "Missing explicit Mac download"
assert "not guaranteed" in html.lower(), "Mac recording limitation must be visible"
print("Website resources, Windows checksum, Mac download and recording disclosure checked")

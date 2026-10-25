"""Package only reviewed runtime files; no tests, caches, signing keys or dependencies."""
import hashlib
import json
from pathlib import Path
import zipfile

root = Path(__file__).resolve().parents[1]
source = root / 'chrome-extension'
output = root / 'website/downloads'
output.mkdir(parents=True, exist_ok=True)
manifest = json.loads((source / 'manifest.json').read_text())
assert manifest['manifest_version'] == 3
assert set(manifest['permissions']) == {'activeTab', 'scripting', 'storage'}
assert not manifest.get('host_permissions') and not manifest.get('content_scripts')
assert "script-src 'self'" in manifest['content_security_policy']['extension_pages']
files = ['manifest.json', 'core.js', 'background.js', 'content.js', 'editor.html', 'editor.css', 'editor.js', 'icon.png', 'README.md']
archive = output / 'NotchPrompter-Chrome.zip'
with zipfile.ZipFile(archive, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=9) as package:
    for name in sorted(files):
        entry = zipfile.ZipInfo('NotchPrompter-Chrome/' + name, date_time=(2026, 10, 25, 0, 0, 0))
        entry.compress_type = zipfile.ZIP_DEFLATED
        entry.external_attr = 0o100644 << 16
        package.writestr(entry, (source / name).read_bytes())
digest = hashlib.sha256(archive.read_bytes()).hexdigest()
metadata = {'version': manifest['version'], 'sha256': digest, 'bytes': archive.stat().st_size}
(output / 'chrome-extension.json').write_text(json.dumps(metadata, indent=2) + '\n')
(output / 'SHA256-Chrome.txt').write_text(digest + '  ' + archive.name + '\n')
print('Packaged Chrome extension:', metadata)

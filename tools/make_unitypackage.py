"""Builds LITTOOL_v<version>.unitypackage (installs to Assets/LITTOOL) from the VPM package in this repository.

usage (from the repository root):  python tools/make_unitypackage.py [out dir]

The package's own .meta files give the GUIDs, so the unitypackage and the VCC package share them. The folder
Assets/LITTOOL itself takes the GUID that package.json's legacyFolders names (the VCC removes that folder when it
installs the package). package.json is not part of the unitypackage.
"""
import gzip
import io
import json
import os
import re
import sys
import tarfile
import time

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
NAME = 'com.litt.littool'
PKG = os.path.join(REPO, 'Packages', NAME)
TARGET = 'Assets/LITTOOL'


def folder_meta(g):
    return (f"fileFormatVersion: 2\nguid: {g}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n"
            f"  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def guid_of(meta_path):
    m = re.search(r'^guid: ([0-9a-f]{32})', open(meta_path, encoding='utf-8').read(), re.M)
    if not m:
        raise SystemExit('no guid in ' + meta_path)
    return m.group(1)


def main():
    out_dir = sys.argv[1] if len(sys.argv) > 1 else REPO
    manifest = json.load(open(os.path.join(PKG, 'package.json'), encoding='utf-8'))
    version = manifest['version']
    root_guid = manifest['legacyFolders']['Assets\\LITTOOL']

    entries = [(TARGET, root_guid, None, folder_meta(root_guid).encode('utf-8'))]
    for dirpath, dirnames, filenames in os.walk(PKG):
        dirnames.sort()
        rel_dir = os.path.relpath(dirpath, PKG).replace(os.sep, '/')
        for d in dirnames:
            rel = d if rel_dir == '.' else f'{rel_dir}/{d}'
            meta = os.path.join(dirpath, d) + '.meta'
            entries.append((f'{TARGET}/{rel}', guid_of(meta), None, open(meta, 'rb').read()))
        for f in sorted(filenames):
            if f.endswith('.meta') or (rel_dir == '.' and f == 'package.json'):
                continue
            rel = f if rel_dir == '.' else f'{rel_dir}/{f}'
            full = os.path.join(dirpath, f)
            entries.append((f'{TARGET}/{rel}', guid_of(full + '.meta'), open(full, 'rb').read(), open(full + '.meta', 'rb').read()))

    out = os.path.join(out_dir, f'LITTOOL_v{version}.unitypackage')
    now = int(time.time())

    def add_bytes(tar, name, data):
        ti = tarfile.TarInfo(name)
        ti.size = len(data)
        ti.mtime = now
        ti.mode = 0o644
        tar.addfile(ti, io.BytesIO(data))

    def add_dir(tar, name):
        ti = tarfile.TarInfo(name)
        ti.type = tarfile.DIRTYPE
        ti.mtime = now
        ti.mode = 0o755
        tar.addfile(ti)

    # Unity on Windows extracts with 7-Zip, which names the inner file after the gzip header's file name: it must end
    # in .tar (Unity's own exporter writes "archtemp.tar"), otherwise the import fails silently.
    with open(out, 'wb') as raw:
        with gzip.GzipFile(filename='archtemp.tar', mode='wb', fileobj=raw, mtime=now) as gz:
            with tarfile.open(fileobj=gz, mode='w', format=tarfile.USTAR_FORMAT) as tar:
                for path, g, asset, meta in entries:
                    add_dir(tar, g)
                    if asset is not None:
                        add_bytes(tar, f'{g}/asset', asset)
                    add_bytes(tar, f'{g}/asset.meta', meta)
                    add_bytes(tar, f'{g}/pathname', path.encode('utf-8'))
    print(out, os.path.getsize(out), 'bytes,', len(entries), 'entries')


if __name__ == '__main__':
    main()

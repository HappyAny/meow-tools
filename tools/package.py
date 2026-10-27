from pathlib import Path
import zipfile,json,hashlib
root=Path(__file__).resolve().parents[1];exe=root/'dist/standalone/喵喵小工具.exe'
source=root/'dist/MeowTools-1.0.0-source.zip';files=[root/n for n in ('README.md','LICENSE','THIRD-PARTY-NOTICES.md','.gitignore')]
for folder in ('src','tests','tools','docs','third-party'):
 files.extend(p for p in (root/folder).rglob('*') if p.is_file() and not {'bin','obj','__pycache__'}.intersection(p.relative_to(root).parts))
with zipfile.ZipFile(source,'w',zipfile.ZIP_DEFLATED,compresslevel=9) as z:
 for p in files:z.write(p,'meow-tools/'+p.relative_to(root).as_posix())
package=root/'dist/MeowTools-1.0.0-win-x64.zip';payload={exe.name:exe,source.name:source,'README.md':root/'README.md','LICENSE':root/'LICENSE','THIRD-PARTY-NOTICES.md':root/'THIRD-PARTY-NOTICES.md'}
for p in files:
 if p.relative_to(root).parts[0] in ('docs','third-party'):payload[p.relative_to(root).as_posix()]=p
manifest=''.join(hashlib.sha256(p.read_bytes()).hexdigest()+'  '+name+'\n' for name,p in sorted(payload.items()))
with zipfile.ZipFile(package,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
 z.writestr('tools/','')
 for name,p in payload.items():z.write(p,name)
 z.writestr('SHA256SUMS.txt',manifest)
for p in (source,package):
 with zipfile.ZipFile(p) as z:assert z.testzip() is None
receipt={'file':package.name,'bytes':package.stat().st_size,'sha256':hashlib.sha256(package.read_bytes()).hexdigest(),'crc_check':'passed','existing_user_tools_included':False}
(root/'dist/package-receipt.json').write_text(json.dumps(receipt,indent=2),encoding='utf8');print(json.dumps(receipt,indent=2))

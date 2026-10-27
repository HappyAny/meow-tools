from pathlib import Path
from io import BytesIO
import argparse,hashlib,json,os,shutil,subprocess,uuid
import pefile
from PIL import Image
root=Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser();parser.add_argument('--tools-dir',type=Path,required=True);args=parser.parse_args()
exe=root/'dist/standalone/喵喵小工具.exe';pe=pefile.PE(str(exe));icons=[]
for entry in pe.DIRECTORY_ENTRY_RESOURCE.entries:
 if entry.id==3:
  for item in entry.directory.entries:
   data=item.directory.entries[0].data.struct
   with Image.open(BytesIO(pe.get_data(data.OffsetToData,data.Size))) as icon:icons.append(icon.width)
assert sorted(icons)==[16,20,24,32,40,48,64,128,256]
trial=root/'artifacts'/('portable-'+uuid.uuid4().hex);app=trial/'app';app.mkdir(parents=True);empty=trial/'no-dotnet';empty.mkdir()
shutil.copy2(exe,app/exe.name); output=trial/'screens'
env=os.environ.copy();env.update(DOTNET_ROOT=str(empty),DOTNET_ROOT_X64=str(empty),DOTNET_MULTILEVEL_LOOKUP='0',DOTNET_BUNDLE_EXTRACT_BASE_DIR=str(trial/'bundle-extract'),
 MEOW_VERIFY_TOOLS=str(args.tools_dir.resolve()),MEOW_VERIFY_OUTPUT=str(output),DOTNET_HOST_TRACE='1',DOTNET_HOST_TRACEFILE=str(trial/'host-trace.txt'))
env['PATH']=str(Path(os.environ['SystemRoot'])/'System32')
completed=subprocess.run([str(app/exe.name),'--verify-native'],cwd=root,env=env,timeout=45,capture_output=True,text=True)
assert completed.returncode==0,(completed.returncode,completed.stderr)
report=json.loads((output/'verification.json').read_text(encoding='utf8'));assert report['passed'] and not report['childToolsStarted'] and report['licenses']==5
trace=(trial/'host-trace.txt').read_text(encoding='utf8');assert 'Executing as a self-contained app' in trace and 'Completed new extraction.' in trace
ui=root/'docs/ui';ui.mkdir(exist_ok=True)
for name in report['captures']:
 with Image.open(output/name) as picture:picture.verify()
 shutil.copy2(output/name,ui/name)
receipt={**report,'exe_sha256':hashlib.sha256(exe.read_bytes()).hexdigest(),'only_exe_copied':True,'runtime_install_required':False,'new_extraction_verified':True,'icons':sorted(icons),'isolation_trial':str(trial)}
(root/'docs/portable-check.json').write_text(json.dumps(receipt,indent=2,ensure_ascii=False),encoding='utf8');print(json.dumps(receipt,indent=2,ensure_ascii=False))

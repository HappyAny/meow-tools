from pathlib import Path
import argparse,hashlib,json,os,subprocess
root=Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser(); parser.add_argument('--offline-feed',type=Path); args=parser.parse_args()
build=root/'artifacts/build'; build.mkdir(parents=True,exist_ok=True)
env=os.environ.copy()
for key,name in {'DOTNET_CLI_HOME':'dotnet-home','NUGET_HTTP_CACHE_PATH':'nuget-http','NUGET_PLUGINS_CACHE_PATH':'nuget-plugins','NUGET_SCRATCH':'nuget-scratch'}.items():env[key]=str(build/name)
env.update(DOTNET_CLI_TELEMETRY_OPTOUT='1',DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1',DOTNET_GENERATE_ASPNET_CERTIFICATE='false')
output=root/'dist/standalone'
cmd=['dotnet','publish',str(root/'src/MeowTools/MeowTools.csproj'),'-c','Release','-r','win-x64','--self-contained','true','-o',str(output),
 '-p:RuntimeFrameworkVersion=10.0.14','-p:PublishSingleFile=true','-p:IncludeNativeLibrariesForSelfExtract=true','-p:EnableCompressionInSingleFile=true',
 '-p:DebugType=embedded','-p:DebugSymbols=false','-p:PublishTrimmed=false','-p:UseSharedCompilation=false','-nodeReuse:false','-p:RestorePackagesPath='+str(build/'packages'),'-v','minimal']
if args.offline_feed:cmd+=['--source',str(args.offline_feed.resolve()),'-p:NuGetAudit=false']
subprocess.run(cmd,cwd=root,env=env,check=True)
exe=output/'喵喵小工具.exe'; assert sorted(p.name for p in output.iterdir())==[exe.name]
runtime=json.loads((root/'src/MeowTools/bin/Release/net10.0-windows/win-x64/喵喵小工具.runtimeconfig.json').read_text())
frameworks=runtime['runtimeOptions']['includedFrameworks']; assert all(x['version']=='10.0.14' for x in frameworks)
receipt={'version':'1.0.0','file':exe.name,'bytes':exe.stat().st_size,'sha256':hashlib.sha256(exe.read_bytes()).hexdigest(),'self_contained':True,'frameworks':frameworks,'distribution_file_count':1}
(root/'docs/build.json').write_text(json.dumps(receipt,indent=2,ensure_ascii=False),encoding='utf8'); print(json.dumps(receipt,indent=2,ensure_ascii=False))

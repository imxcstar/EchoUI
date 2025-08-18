import { dotnet } from './_framework/dotnet.js'
import { dom } from './dom.js'

const { setModuleImports, getAssemblyExports, getConfig, runMain } = await dotnet
    .withApplicationArguments("start")
    .create();

setModuleImports('dom', {
    dom
});

const config = getConfig();
const exports = await getAssemblyExports(config.mainAssemblyName);
window.EchoUIHelper = exports.EchoUIHelper;

await runMain();
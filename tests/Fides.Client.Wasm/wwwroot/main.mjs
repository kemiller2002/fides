// Starts the .NET WebAssembly runtime under Node and runs Program.main.
import { dotnet } from './_framework/dotnet.js';

const exitCode = await dotnet.run();
process.exit(exitCode);

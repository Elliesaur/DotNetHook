# DotNetHook
A hook proof of concept with no native dependencies. 
You can hook .NET and Native (in progress) methods without using a C++ DLL or some other native project.

# Features
* Hook a .NET Method to another .NET Method.
* Hook a Native export (MessageBoxA, kernel32 exports) to a .NET Method.
* Call the original method (native or managed) with ease.
* x86 and x64 supported.
* Disposable hooks, clean up is done.

# Bugs
* Are you sure it is a bug? Things like the "this" keyword and instances of objects can get a bit strange when you're hooking another instance based method! This may not be this!

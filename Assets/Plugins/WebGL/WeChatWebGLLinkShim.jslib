// The WeChat Mini Game SDK v0.1.32 runtime references this bridge, but its
// bundled SDK-Call-JS.jslib does not define it. The runtime DLL is preserved
// for regular WebGL builds, so Emscripten still needs the symbol even though
// EchoRun only calls the SDK in WeChat Mini Game builds.
mergeInto(LibraryManager.library, {
  WX_SyncFunction_tnnt: function(functionName, returnType, param1, param2, param3) {
    var sdk = typeof window !== 'undefined' ? window.WXWASMSDK : null;
    var fn = sdk && sdk.WX_SyncFunction_tnnt;
    var result = fn
      ? fn(
          _WXPointer_stringify_adaptor(functionName),
          _WXPointer_stringify_adaptor(returnType),
          param1,
          param2,
          _WXPointer_stringify_adaptor(param3))
      : '';
    var bufferSize = lengthBytesUTF8(result || '') + 1;
    var buffer = _malloc(bufferSize);
    stringToUTF8(result || '', buffer, bufferSize);
    return buffer;
  }
});

mergeInto(LibraryManager.library, {
  EchoRun_SetWebFrameRate__deps: ['$Browser', 'emscripten_set_main_loop_timing'],
  EchoRun_SetWebFrameRate: function (fps) {
    // Tuanjie 2022 maps 60 FPS to one rAF, which runs at 165 FPS on a
    // 165 Hz display. Use Emscripten's timed mode for explicit FPS choices.
    var interval = 1000 / fps;
    if (!Browser.mainLoop.func) return 1;
    if (Browser.mainLoop.timingMode === 0 &&
        Browser.mainLoop.timingValue === interval) return 0;
    return _emscripten_set_main_loop_timing(0, interval);
  }
});

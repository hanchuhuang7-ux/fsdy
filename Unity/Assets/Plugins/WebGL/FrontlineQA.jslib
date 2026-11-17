// Only called by the opt-in, loopback-only visual acceptance runner.
mergeInto(LibraryManager.library, {
  FrontlineQAInit__deps: ['$GL'],
  FrontlineQAInit: function () {
    var gl = GL.currentContext.GLctx;
    if (gl.__frontlineQA) return;
    gl.__frontlineQA = true; gl.__frontlineDraws = 0;
    ['drawArrays','drawElements','drawArraysInstanced','drawElementsInstanced'].forEach(function (name) {
      var original = gl[name]; if (!original) return;
      gl[name] = function () { gl.__frontlineDraws++; return original.apply(gl, arguments); };
    });
  },
  FrontlineQABatches__deps: ['$GL'],
  FrontlineQABatches: function () {
    var gl = GL.currentContext.GLctx, count = gl.__frontlineDraws || 0;
    gl.__frontlineDraws = 0; return count;
  }
});

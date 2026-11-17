// Deliberately never responds. Used to verify subprocess timeout fallback.
process.stdin.resume();
setInterval(()=>{},10000);

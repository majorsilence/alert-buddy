// The browser's sound for the demo: one HTMLAudioElement per sound, and one looping element for the siren. A browser only lets a page make
// sound after the person has touched it, which a click on Start practice or a sound button always is; before that, play () is refused and
// the page stays quiet rather than raising an error.
globalThis.alertBuddySound = (() => {
    let loop = null;

    function make(url, looping, volume) {
        const audio = new Audio(url);
        audio.loop = looping;
        audio.volume = Math.max(0, Math.min(1, volume));
        return audio;
    }

    return {
        play(url, volume) {
            make(url, false, volume).play().catch(() => { });
        },
        startLoop(url, volume) {
            this.stopLoop();
            loop = make(url, true, volume);
            loop.play().catch(() => { });
        },
        stopLoop() {
            if (loop) {
                loop.pause();
                loop = null;
            }
        },
    };
})();

(() => {
    const preferenceKey = "reportNotifications.enabled";
    const AudioContext = window.AudioContext || window.webkitAudioContext;
    let enabled = true;
    let audio;

    function unlockAudio() {
        if (!AudioContext || audio?.state === "running") return;
        try {
            audio ??= new AudioContext();
            audio.resume().catch(() => {});
        } catch {
        }
    }

    window.addEventListener("click", unlockAudio, true);
    window.addEventListener("keydown", unlockAudio, true);

    window.reportNotifications = {
        isEnabled: () => {
            try {
                return localStorage.getItem(preferenceKey) !== "false";
            } catch {
                return enabled;
            }
        },
        setEnabled: (value) => {
            enabled = value;
            try {
                localStorage.setItem(preferenceKey, String(value));
            } catch {
            }
        },
        playSound: () => {
            if (audio?.state !== "running") return;
            const start = audio.currentTime;
            const tone = audio.createOscillator();
            const gain = audio.createGain();
            tone.frequency.value = 880;
            gain.gain.setValueAtTime(0, start);
            gain.gain.linearRampToValueAtTime(0.12, start + 0.02);
            gain.gain.exponentialRampToValueAtTime(0.001, start + 0.25);
            tone.connect(gain);
            gain.connect(audio.destination);
            tone.onended = () => {
                tone.disconnect();
                gain.disconnect();
            };
            tone.start(start);
            tone.stop(start + 0.25);
        }
    };
})();

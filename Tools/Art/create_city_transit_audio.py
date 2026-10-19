"""Create an original, seamless, quiet electric-rail ambience with stdlib only.

All oscillator frequencies are integer multiples of 1/duration, including the
slow wheel-noise envelope. The PCM loop therefore has no edit or fade seam.
"""
import hashlib
import json
import math
import random
import struct
import wave
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "Assets/Art/StackedCity/Audio/SkyRailLoop.wav"
RATE = 22050
DURATION = 4


def render():
    rng = random.Random(20260912)
    count = RATE * DURATION
    # A periodic, band-limited random spectrum supplies wheel/rail texture.
    # Roll off the high frequencies so the background cannot resemble a horn.
    bins = sorted(rng.sample(range(280, 5601), 96))
    noise = []
    for index in bins:
        frequency = index / DURATION
        weight = 1.0 / (1.0 + (frequency / 340.0) ** 2)
        noise.append((math.tau * frequency, rng.uniform(0, math.tau), weight))
    normalization = math.sqrt(sum(weight * weight for _, _, weight in noise))
    samples = []
    for frame in range(count):
        t = frame / RATE
        rolling = sum(math.sin(omega * t + phase) * weight
                      for omega, phase, weight in noise) / normalization
        envelope = .84 + .08 * math.sin(math.tau * t) + .035 * math.sin(math.tau * 3 * t)
        motor = (.18 * math.sin(math.tau * 47.5 * t)
                 + .075 * math.sin(math.tau * 95 * t + .35)
                 + .035 * math.sin(math.tau * 142.5 * t + .8))
        samples.append(motor + .17 * rolling * envelope)
    # Remove numerical DC drift and leave generous digital headroom.
    mean = sum(samples) / count
    samples = [sample - mean for sample in samples]
    gain = .42 / max(abs(sample) for sample in samples)
    pcm = [round(sample * gain * 32767) for sample in samples]
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(OUTPUT), "wb") as writer:
        writer.setnchannels(1)
        writer.setsampwidth(2)
        writer.setframerate(RATE)
        writer.writeframes(struct.pack("<" + "h" * count, *pcm))
    with wave.open(str(OUTPUT), "rb") as reader:
        assert reader.getnchannels() == 1
        assert reader.getframerate() == RATE
        assert reader.getnframes() == count
    maximum_step = max(abs(pcm[i] - pcm[i - 1]) for i in range(1, count))
    seam_step = abs(pcm[0] - pcm[-1])
    assert seam_step <= maximum_step, "Loop seam exceeds a normal waveform step"
    assert max(abs(sample) for sample in pcm) < 32767, "PCM clipped"
    print(json.dumps({
        "output": str(OUTPUT), "duration_seconds": DURATION, "sample_rate": RATE,
        "channels": 1, "pcm_bits": 16, "peak": max(abs(v) for v in pcm) / 32767,
        "rms": math.sqrt(sum(v * v for v in pcm) / count) / 32767,
        "loop_seam_step": seam_step, "maximum_sample_step": maximum_step,
        "bytes": OUTPUT.stat().st_size, "sha256": hashlib.sha256(OUTPUT.read_bytes()).hexdigest()
    }, indent=2))


if __name__ == "__main__":
    render()

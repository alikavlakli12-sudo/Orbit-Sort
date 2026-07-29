# Performance

Orbit Sort includes a repeatable live ring-rotation benchmark at:

```text
Orbit Sort/Profile Live Ring Rotation
```

The benchmark enters Play Mode, warms the renderer, records an idle
baseline, continuously rotates the outer ring, records the snap, writes a
JSON report to `Library/Profiling/`, and leaves Play Mode automatically.

## July 29, 2026 rendering pass

Environment:

- Unity `6000.4.0f1`
- macOS Editor using the Metal renderer
- Stable 60 FPS frame pacing
- 180 measured baseline frames
- 240 measured continuous-rotation frames
- 90 measured snap frames

| Rotation metric | Before | After |
|---|---:|---:|
| Mean frame time | 23.83 ms | 16.66 ms |
| P95 frame time | 38.03 ms | 19.22 ms |
| Maximum frame time | 41.87 ms | 20.77 ms |
| Frames over 20 ms | 179 / 240 | 1 / 240 |
| Median non-zero SetPass count | 260 | 95 |
| Median non-zero submitted triangles | 1,961,704 | 865,380 |

The measured bottleneck was multi-pass rendering, not the ring transform.
The correction keeps imported board geometry persistent, statically
batches non-moving board assets, renders studio lighting in one forward
pass, uses GPU instancing for repeated marbles, removes unnecessary shadow
cascades, and keeps only marble transforms dynamic.

These Editor measurements are a reproducible development baseline. A
device build should also be profiled before release because thermal state,
screen resolution, and mobile GPU generation affect final headroom.

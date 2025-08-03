<p align="center">
  <img width="456" alt="Screenshot" src="https://github.com/user-attachments/assets/36c072bb-0aa6-4c09-8cd6-ab533a53060f" />
</p>

This is an [ISF shader](https://isf.video) for simulating the slime mold
_Physarum polycephalum_. The simulation is based “Characteristics of pattern
formation and evolution in approximations of physarum transport networks” by
Jeff Jones (PDF available [here](https://uwe-repository.worktribe.com/output/980579)),
and [this webpage by Sage Johnson](https://cargocollective.com/sagejenson/physarum)
is another excellent resource. This particular shader is converted from
[this ShaderToy shader](https://www.shadertoy.com/view/tlKGDh) by
[**@MichaelMoroz**](https://github.com/MichaelMoroz).

This is a multi-pass shader that is intended to be used with floating-point
buffers. Not all ISF hosts support floating-point buffers; for example,
https://editor.isf.video/ does not appear to support floating-point buffers.
[Videosync](https://videosync.showsync.com), on the other hand, supports
floating-point buffers in v2.0.12 or later (a beta version of v2.0.12 is
available
[here](https://forum.showsync.com/t/floating-point-buffers-in-isf-shaders/2490/7)).
Note that this shader will produce *very* different output if floating-point
buffers are not used.

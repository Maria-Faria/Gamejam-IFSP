# Cute Science — sound effects pack

182 individual original synthesized WAV effects. 48 kHz, 16-bit PCM, mono. No speech, sampled voices, realistic screams, or harsh gunshots.

## Character sound palettes

| Character | Sound palette |
|---|---|
| Gladys West | Warm navigation chimes and soft rubbery taps |
| Alice Ball | Bubbly glass droplets and rounded pops |
| Barbara McClintock | Wooden seed clicks and gentle marimba notes |
| Ada Lovelace | Clockwork ticks and delicate music-box notes |
| Wanda Díaz-Merced | Smooth resonant pulses and rising star-like chirps |
| Tu Youyou | Bamboo-like taps and light herbal-bottle plinks |
| Patricia Bath | Clear lens pings and tiny glass twinkles |
| Annie Easley | Rounded computer blips and springy pulses |
| Bibha Chowdhuri | Paired detector clicks and short crystalline pings |
| Hertha Ayrton | Soft electrical zips and low rounded hums |
| Mária Telkes | Warm solar chimes and buoyant rounded notes |
| Chien-Shiung Wu | Precise paired plinks and clean resonant taps |

Every scientist has: idle, use_button, use_item, run, shoot, projectile_hit_wall, projectile_explode_wall, projectile_hit_metal, projectile_hit_wood, projectile_hit_plastic, being_hit, death, happy.

Projectile cues are character-specific starting weapon palettes; if weapons are interchangeable, select impacts by the equipped weapon instead. These are creative sound identities, not claims about the scientists.

## Shared effects

- `ui/keyboard_click.wav`: Soft keyboard key tak.
- `ui/mouse_click.wav`: Small press-and-release mouse click.
- `electronics/light_on.wav`: Switch click followed by a soft rising light tone.
- `ui/victory.wav`: Bright five-note victory jingle.
- `ui/defeat.wav`: Gentle descending defeat jingle.
- `ui/emoji_happy.wav`: Smiling three-note bubble.
- `ui/emoji_sad.wav`: Soft drooping two-note sigh.
- `ui/emoji_angry.wav`: Cute grumbly triplet.
- `ui/negative.wav`: Calm two-note refusal.
- `ui/wrong.wav`: Short gently dissonant wobble.
- `ui/correct.wav`: Clean upward confirmation ding.
- `ui/right.wav`: Single bright acknowledgment pop.
- `ui/warning.wav`: Three clear alert pips without a harsh alarm.
- `ghost/idle.wav`: Airy wobbling ghost coo.
- `ghost/roar.wav`: Comical low ghost warble.
- `ghost/appear.wav`: Rising spectral bloop and airy swish.
- `ghost/slither.wav`: Soft undulating shuffles and low burbles.
- `ghost/puff.wav`: Short cloudy pop.
- `ghost/death.wav`: Dissolving descending bubbles and a final poof.
- `ghost/spit.wav`: Wet cartoon plip with a small launch chirp.
- `ghost/explosion.wav`: Cushioned ghost burst with wobbling debris.
- `ghost/pain.wav`: Brief downward squeaky warble.
- `environment/flames.wav`: Soft filtered flame flutter with tiny crackles; seamless two-second loop.
- `electronics/beeping.wav`: Three mellow equipment status beeps.
- `electronics/lights.wav`: Delicate electronic light shimmer.
- `electronics/beep_boop.wav`: Friendly high-low robot response.

## Integration

- Run files: 0.88-second loops, four footsteps (about 273 steps/minute). Trigger individual steps from animation instead if desired.
- Flames: two-second loop. Use a 10–20 ms crossfade at the join for continuous ambience.
- Idle files are occasional one-shots, not continuous loops. Ghost slither is a one-shot.
- All other files are one-shots with short fades. Set UI volume below weapons and reactions.
- Peak headroom is approximately -4 dBFS for one-shots and -7.5 dBFS for loops. Simultaneous sounds still need mixer headroom.
- Slight random pitch (+/-3%) and volume variation works well for repeated shots and impacts.
- This folder includes a CSV and JSON mapping every individual file to its event, description, duration, and loop setting.

## Complete file list

- `characters/gladys_west/idle.wav` — Gladys West: Quiet two-note idle murmur; play occasionally.
- `characters/gladys_west/use_button.wav` — Gladys West: Mitten button tap with a tiny confirmation note.
- `characters/gladys_west/use_item.wav` — Gladys West: Three-note discovery sparkle.
- `characters/gladys_west/run.wav` — Gladys West: Four alternating padded footsteps; seamless 0.88-second loop.
- `characters/gladys_west/shoot.wav` — Gladys West: Rounded light projectile pew.
- `characters/gladys_west/projectile_hit_wall.wav` — Gladys West: Soft stone tick and muted dust thump.
- `characters/gladys_west/projectile_explode_wall.wav` — Gladys West: Small cushioned poof with falling light debris.
- `characters/gladys_west/projectile_hit_metal.wav` — Gladys West: Short bright toy-metal ting.
- `characters/gladys_west/projectile_hit_wood.wav` — Gladys West: Hollow wooden tok.
- `characters/gladys_west/projectile_hit_plastic.wav` — Gladys West: Light springy plastic plok.
- `characters/gladys_west/being_hit.wav` — Gladys West: Quick descending nonverbal ouch cue.
- `characters/gladys_west/death.wav` — Gladys West: Gentle four-note power-down; no realistic distress.
- `characters/gladys_west/happy.wav` — Gladys West: Bouncy nonverbal celebration.
- `characters/alice_ball/idle.wav` — Alice Ball: Quiet two-note idle murmur; play occasionally.
- `characters/alice_ball/use_button.wav` — Alice Ball: Mitten button tap with a tiny confirmation note.
- `characters/alice_ball/use_item.wav` — Alice Ball: Three-note discovery sparkle.
- `characters/alice_ball/run.wav` — Alice Ball: Four alternating padded footsteps; seamless 0.88-second loop.
- `characters/alice_ball/shoot.wav` — Alice Ball: Rounded light projectile pew.
- `characters/alice_ball/projectile_hit_wall.wav` — Alice Ball: Soft stone tick and muted dust thump.
- `characters/alice_ball/projectile_explode_wall.wav` — Alice Ball: Small cushioned poof with falling light debris.
- `characters/alice_ball/projectile_hit_metal.wav` — Alice Ball: Short bright toy-metal ting.
- `characters/alice_ball/projectile_hit_wood.wav` — Alice Ball: Hollow wooden tok.
- `characters/alice_ball/projectile_hit_plastic.wav` — Alice Ball: Light springy plastic plok.
- `characters/alice_ball/being_hit.wav` — Alice Ball: Quick descending nonverbal ouch cue.
- `characters/alice_ball/death.wav` — Alice Ball: Gentle four-note power-down; no realistic distress.
- `characters/alice_ball/happy.wav` — Alice Ball: Bouncy nonverbal celebration.
- `characters/barbara_mcclintock/idle.wav` — Barbara McClintock: Quiet two-note idle murmur; play occasionally.
- `characters/barbara_mcclintock/use_button.wav` — Barbara McClintock: Mitten button tap with a tiny confirmation note.
- `characters/barbara_mcclintock/use_item.wav` — Barbara McClintock: Three-note discovery sparkle.
- `characters/barbara_mcclintock/run.wav` — Barbara McClintock: Four alternating padded footsteps; seamless 0.88-second loop.
- `characters/barbara_mcclintock/shoot.wav` — Barbara McClintock: Rounded light projectile pew.
- `characters/barbara_mcclintock/projectile_hit_wall.wav` — Barbara McClintock: Soft stone tick and muted dust thump.
- `characters/barbara_mcclintock/projectile_explode_wall.wav` — Barbara McClintock: Small cushioned poof with falling light debris.
- `characters/barbara_mcclintock/projectile_hit_metal.wav` — Barbara McClintock: Short bright toy-metal ting.
- `characters/barbara_mcclintock/projectile_hit_wood.wav` — Barbara McClintock: Hollow wooden tok.
- `characters/barbara_mcclintock/projectile_hit_plastic.wav` — Barbara McClintock: Light springy plastic plok.
- `characters/barbara_mcclintock/being_hit.wav` — Barbara McClintock: Quick descending nonverbal ouch cue.
- `characters/barbara_mcclintock/death.wav` — Barbara McClintock: Gentle four-note power-down; no realistic distress.
- `characters/barbara_mcclintock/happy.wav` — Barbara McClintock: Bouncy nonverbal celebration.
- `characters/ada_lovelace/idle.wav` — Ada Lovelace: Quiet two-note idle murmur; play occasionally.
- `characters/ada_lovelace/use_button.wav` — Ada Lovelace: Mitten button tap with a tiny confirmation note.
- `characters/ada_lovelace/use_item.wav` — Ada Lovelace: Three-note discovery sparkle.
- `characters/ada_lovelace/run.wav` — Ada Lovelace: Four alternating padded footsteps; seamless 0.88-second loop.
- `characters/ada_lovelace/shoot.wav` — Ada Lovelace: Rounded light projectile pew.
- `characters/ada_lovelace/projectile_hit_wall.wav` — Ada Lovelace: Soft stone tick and muted dust thump.
- `characters/ada_lovelace/projectile_explode_wall.wav` — Ada Lovelace: Small cushioned poof with falling light debris.
- `characters/ada_lovelace/projectile_hit_metal.wav` — Ada Lovelace: Short bright toy-metal ting.
- `characters/ada_lovelace/projectile_hit_wood.wav` — Ada Lovelace: Hollow wooden tok.
- `characters/ada_lovelace/projectile_hit_plastic.wav` — Ada Lovelace: Light springy plastic plok.
- `characters/ada_lovelace/being_hit.wav` — Ada Lovelace: Quick descending nonverbal ouch cue.
- `characters/ada_lovelace/death.wav` — Ada Lovelace: Gentle four-note power-down; no realistic distress.
- `characters/ada_lovelace/happy.wav` — Ada Lovelace: Bouncy nonverbal celebration.
- `characters/wanda_diaz_merced/idle.wav` — Wanda Díaz-Merced: Quiet two-note idle murmur; play occasionally.
- `characters/wanda_diaz_merced/use_button.wav` — Wanda Díaz-Merced: Mitten button tap with a tiny confirmation note.
- `characters/wanda_diaz_merced/use_item.wav` — Wanda Díaz-Merced: Three-note discovery sparkle.
- `characters/wanda_diaz_merced/run.wav` — Wanda Díaz-Merced: Four alternating padded footsteps; seamless 0.88-second loop.
- `characters/wanda_diaz_merced/shoot.wav` — Wanda Díaz-Merced: Rounded light projectile pew.
- `characters/wanda_diaz_merced/projectile_hit_wall.wav` — Wanda Díaz-Merced: Soft stone tick and muted dust thump.
- `characters/wanda_diaz_merced/projectile_explode_wall.wav` — Wanda Díaz-Merced: Small cushioned poof with falling light debris.
- `characters/wanda_diaz_merced/projectile_hit_metal.wav` — Wanda Díaz-Merced: Short bright toy-metal ting.
- `characters/wanda_diaz_merced/projectile_hit_wood.wav` — Wanda Díaz-Merced: Hollow wooden tok.
- `characters/wanda_diaz_merced/projectile_hit_plastic.wav` — Wanda Díaz-Merced: Light springy plastic plok.
- `characters/wanda_diaz_merced/being_hit.wav` — Wanda Díaz-Merced: Quick descending nonverbal ouch cue.
- `characters/wanda_diaz_merced/death.wav` — Wanda Díaz-Merced: Gentle four-note power-down; no realistic distress.
- `characters/wanda_diaz_merced/happy.wav` — Wanda Díaz-Merced: Bouncy nonverbal celebration.
- `characters/tu_youyou/idle.wav` — Tu Youyou: Quiet two-note idle murmur; play occasionally.
- `characters/tu_youyou/use_button.wav` — Tu Youyou: Mitten button tap with a tiny confirmation note.
- `characters/tu_youyou/use_item.wav` — Tu Youyou: Three-note discovery sparkle.
- `characters/tu_youyou/run.wav` — Tu Youyou: Four alternating padded footsteps; seamless 0.88-second loop.
- `characters/tu_youyou/shoot.wav` — Tu Youyou: Rounded light projectile pew.
- `characters/tu_youyou/projectile_hit_wall.wav` — Tu Youyou: Soft stone tick and muted dust thump.
- `characters/tu_youyou/projectile_explode_wall.wav` — Tu Youyou: Small cushioned poof with falling light debris.
- `characters/tu_youyou/projectile_hit_metal.wav` — Tu Youyou: Short bright toy-metal ting.
- `characters/tu_youyou/projectile_hit_wood.wav` — Tu Youyou: Hollow wooden tok.
- `characters/tu_youyou/projectile_hit_plastic.wav` — Tu Youyou: Light springy plastic plok.
- `characters/tu_youyou/being_hit.wav` — Tu Youyou: Quick descending nonverbal ouch cue.
- `characters/tu_youyou/death.wav` — Tu Youyou: Gentle four-note power-down; no realistic distress.
- `characters/tu_youyou/happy.wav` — Tu Youyou: Bouncy nonverbal celebration.
- `characters/patricia_bath/idle.wav` — Patricia Bath: Quiet two-note idle murmur; play occasionally.
- `characters/patricia_bath/use_button.wav` — Patricia Bath: Mitten button tap with a tiny confirmation note.
- `characters/patricia_bath/use_item.wav` — Patricia Bath: Three-note discovery sparkle.
- `characters/patricia_bath/run.wav` — Patricia Bath: Four alternating padded footsteps; seamless 0.88-second loop.
- `characters/patricia_bath/shoot.wav` — Patricia Bath: Rounded light projectile pew.
- `characters/patricia_bath/projectile_hit_wall.wav` — Patricia Bath: Soft stone tick and muted dust thump.
- `characters/patricia_bath/projectile_explode_wall.wav` — Patricia Bath: Small cushioned poof with falling light debris.
- `characters/patricia_bath/projectile_hit_metal.wav` — Patricia Bath: Short bright toy-metal ting.
- `characters/patricia_bath/projectile_hit_wood.wav` — Patricia Bath: Hollow wooden tok.
- `characters/patricia_bath/projectile_hit_plastic.wav` — Patricia Bath: Light springy plastic plok.
- `characters/patricia_bath/being_hit.wav` — Patricia Bath: Quick descending nonverbal ouch cue.
- `characters/patricia_bath/death.wav` — Patricia Bath: Gentle four-note power-down; no realistic distress.
- `characters/patricia_bath/happy.wav` — Patricia Bath: Bouncy nonverbal celebration.
- `characters/annie_easley/idle.wav` — Annie Easley: Quiet two-note idle murmur; play occasionally.
- `characters/annie_easley/use_button.wav` — Annie Easley: Mitten button tap with a tiny confirmation note.
- `characters/annie_easley/use_item.wav` — Annie Easley: Three-note discovery sparkle.
- `characters/annie_easley/run.wav` — Annie Easley: Four alternating padded footsteps; seamless 0.88-second loop.
- `characters/annie_easley/shoot.wav` — Annie Easley: Rounded light projectile pew.
- `characters/annie_easley/projectile_hit_wall.wav` — Annie Easley: Soft stone tick and muted dust thump.
- `characters/annie_easley/projectile_explode_wall.wav` — Annie Easley: Small cushioned poof with falling light debris.
- `characters/annie_easley/projectile_hit_metal.wav` — Annie Easley: Short bright toy-metal ting.
- `characters/annie_easley/projectile_hit_wood.wav` — Annie Easley: Hollow wooden tok.
- `characters/annie_easley/projectile_hit_plastic.wav` — Annie Easley: Light springy plastic plok.
- `characters/annie_easley/being_hit.wav` — Annie Easley: Quick descending nonverbal ouch cue.
- `characters/annie_easley/death.wav` — Annie Easley: Gentle four-note power-down; no realistic distress.
- `characters/annie_easley/happy.wav` — Annie Easley: Bouncy nonverbal celebration.
- `characters/bibha_chowdhuri/idle.wav` — Bibha Chowdhuri: Quiet two-note idle murmur; play occasionally.
- `characters/bibha_chowdhuri/use_button.wav` — Bibha Chowdhuri: Mitten button tap with a tiny confirmation note.
- `characters/bibha_chowdhuri/use_item.wav` — Bibha Chowdhuri: Three-note discovery sparkle.
- `characters/bibha_chowdhuri/run.wav` — Bibha Chowdhuri: Four alternating padded footsteps; seamless 0.88-second loop.
- `characters/bibha_chowdhuri/shoot.wav` — Bibha Chowdhuri: Rounded light projectile pew.
- `characters/bibha_chowdhuri/projectile_hit_wall.wav` — Bibha Chowdhuri: Soft stone tick and muted dust thump.
- `characters/bibha_chowdhuri/projectile_explode_wall.wav` — Bibha Chowdhuri: Small cushioned poof with falling light debris.
- `characters/bibha_chowdhuri/projectile_hit_metal.wav` — Bibha Chowdhuri: Short bright toy-metal ting.
- `characters/bibha_chowdhuri/projectile_hit_wood.wav` — Bibha Chowdhuri: Hollow wooden tok.
- `characters/bibha_chowdhuri/projectile_hit_plastic.wav` — Bibha Chowdhuri: Light springy plastic plok.
- `characters/bibha_chowdhuri/being_hit.wav` — Bibha Chowdhuri: Quick descending nonverbal ouch cue.
- `characters/bibha_chowdhuri/death.wav` — Bibha Chowdhuri: Gentle four-note power-down; no realistic distress.
- `characters/bibha_chowdhuri/happy.wav` — Bibha Chowdhuri: Bouncy nonverbal celebration.
- `characters/hertha_ayrton/idle.wav` — Hertha Ayrton: Quiet two-note idle murmur; play occasionally.
- `characters/hertha_ayrton/use_button.wav` — Hertha Ayrton: Mitten button tap with a tiny confirmation note.
- `characters/hertha_ayrton/use_item.wav` — Hertha Ayrton: Three-note discovery sparkle.
- `characters/hertha_ayrton/run.wav` — Hertha Ayrton: Four alternating padded footsteps; seamless 0.88-second loop.
- `characters/hertha_ayrton/shoot.wav` — Hertha Ayrton: Rounded light projectile pew.
- `characters/hertha_ayrton/projectile_hit_wall.wav` — Hertha Ayrton: Soft stone tick and muted dust thump.
- `characters/hertha_ayrton/projectile_explode_wall.wav` — Hertha Ayrton: Small cushioned poof with falling light debris.
- `characters/hertha_ayrton/projectile_hit_metal.wav` — Hertha Ayrton: Short bright toy-metal ting.
- `characters/hertha_ayrton/projectile_hit_wood.wav` — Hertha Ayrton: Hollow wooden tok.
- `characters/hertha_ayrton/projectile_hit_plastic.wav` — Hertha Ayrton: Light springy plastic plok.
- `characters/hertha_ayrton/being_hit.wav` — Hertha Ayrton: Quick descending nonverbal ouch cue.
- `characters/hertha_ayrton/death.wav` — Hertha Ayrton: Gentle four-note power-down; no realistic distress.
- `characters/hertha_ayrton/happy.wav` — Hertha Ayrton: Bouncy nonverbal celebration.
- `characters/maria_telkes/idle.wav` — Mária Telkes: Quiet two-note idle murmur; play occasionally.
- `characters/maria_telkes/use_button.wav` — Mária Telkes: Mitten button tap with a tiny confirmation note.
- `characters/maria_telkes/use_item.wav` — Mária Telkes: Three-note discovery sparkle.
- `characters/maria_telkes/run.wav` — Mária Telkes: Four alternating padded footsteps; seamless 0.88-second loop.
- `characters/maria_telkes/shoot.wav` — Mária Telkes: Rounded light projectile pew.
- `characters/maria_telkes/projectile_hit_wall.wav` — Mária Telkes: Soft stone tick and muted dust thump.
- `characters/maria_telkes/projectile_explode_wall.wav` — Mária Telkes: Small cushioned poof with falling light debris.
- `characters/maria_telkes/projectile_hit_metal.wav` — Mária Telkes: Short bright toy-metal ting.
- `characters/maria_telkes/projectile_hit_wood.wav` — Mária Telkes: Hollow wooden tok.
- `characters/maria_telkes/projectile_hit_plastic.wav` — Mária Telkes: Light springy plastic plok.
- `characters/maria_telkes/being_hit.wav` — Mária Telkes: Quick descending nonverbal ouch cue.
- `characters/maria_telkes/death.wav` — Mária Telkes: Gentle four-note power-down; no realistic distress.
- `characters/maria_telkes/happy.wav` — Mária Telkes: Bouncy nonverbal celebration.
- `characters/chien_shiung_wu/idle.wav` — Chien-Shiung Wu: Quiet two-note idle murmur; play occasionally.
- `characters/chien_shiung_wu/use_button.wav` — Chien-Shiung Wu: Mitten button tap with a tiny confirmation note.
- `characters/chien_shiung_wu/use_item.wav` — Chien-Shiung Wu: Three-note discovery sparkle.
- `characters/chien_shiung_wu/run.wav` — Chien-Shiung Wu: Four alternating padded footsteps; seamless 0.88-second loop.
- `characters/chien_shiung_wu/shoot.wav` — Chien-Shiung Wu: Rounded light projectile pew.
- `characters/chien_shiung_wu/projectile_hit_wall.wav` — Chien-Shiung Wu: Soft stone tick and muted dust thump.
- `characters/chien_shiung_wu/projectile_explode_wall.wav` — Chien-Shiung Wu: Small cushioned poof with falling light debris.
- `characters/chien_shiung_wu/projectile_hit_metal.wav` — Chien-Shiung Wu: Short bright toy-metal ting.
- `characters/chien_shiung_wu/projectile_hit_wood.wav` — Chien-Shiung Wu: Hollow wooden tok.
- `characters/chien_shiung_wu/projectile_hit_plastic.wav` — Chien-Shiung Wu: Light springy plastic plok.
- `characters/chien_shiung_wu/being_hit.wav` — Chien-Shiung Wu: Quick descending nonverbal ouch cue.
- `characters/chien_shiung_wu/death.wav` — Chien-Shiung Wu: Gentle four-note power-down; no realistic distress.
- `characters/chien_shiung_wu/happy.wav` — Chien-Shiung Wu: Bouncy nonverbal celebration.
- `shared/ui/keyboard_click.wav` — Soft keyboard key tak.
- `shared/ui/mouse_click.wav` — Small press-and-release mouse click.
- `shared/electronics/light_on.wav` — Switch click followed by a soft rising light tone.
- `shared/ui/victory.wav` — Bright five-note victory jingle.
- `shared/ui/defeat.wav` — Gentle descending defeat jingle.
- `shared/ui/emoji_happy.wav` — Smiling three-note bubble.
- `shared/ui/emoji_sad.wav` — Soft drooping two-note sigh.
- `shared/ui/emoji_angry.wav` — Cute grumbly triplet.
- `shared/ui/negative.wav` — Calm two-note refusal.
- `shared/ui/wrong.wav` — Short gently dissonant wobble.
- `shared/ui/correct.wav` — Clean upward confirmation ding.
- `shared/ui/right.wav` — Single bright acknowledgment pop.
- `shared/ui/warning.wav` — Three clear alert pips without a harsh alarm.
- `shared/ghost/idle.wav` — Airy wobbling ghost coo.
- `shared/ghost/roar.wav` — Comical low ghost warble.
- `shared/ghost/appear.wav` — Rising spectral bloop and airy swish.
- `shared/ghost/slither.wav` — Soft undulating shuffles and low burbles.
- `shared/ghost/puff.wav` — Short cloudy pop.
- `shared/ghost/death.wav` — Dissolving descending bubbles and a final poof.
- `shared/ghost/spit.wav` — Wet cartoon plip with a small launch chirp.
- `shared/ghost/explosion.wav` — Cushioned ghost burst with wobbling debris.
- `shared/ghost/pain.wav` — Brief downward squeaky warble.
- `shared/environment/flames.wav` — Soft filtered flame flutter with tiny crackles; seamless two-second loop.
- `shared/electronics/beeping.wav` — Three mellow equipment status beeps.
- `shared/electronics/lights.wav` — Delicate electronic light shimmer.
- `shared/electronics/beep_boop.wav` — Friendly high-low robot response.
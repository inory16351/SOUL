// Motion data for the ARM-BLADE wanderer. The right forearm is the blade, the left arm is free
// and is used for balance. Angles in degrees, local Euler XYZ (see limits.js):
//   pelvis/spine/chest  +X = lean forward, +Y = turn right
//   thigh -X = leg forward, shin +X = knee bend, foot +X = toes down
//   left upper arm: -X = forward, -Z = out to the side
// Body offsets are pelvis offsets in root space on top of the crouched stance.
// Weapon keys (bottom of file): [time, upper-arm direction, blade direction] in root space
// (or chest space when weaponSpace is 'chest'). `yaw` curves spin the whole body (degrees).
//
// Tempo: quick, fluid one-arm cuts. Wind-ups are short and coiled, the act lasts ~0.07 s,
// the strike pose is held, each cut flows into the next from where the last one ended.
export const BASE_BODY = [0, -.07, 0];
export const ARM_BLADE_BLOCKERS = ['pelvis', 'belly', 'chest', 'neck', 'head', 'Lthigh', 'Rthigh', 'Lshin', 'Rshin', 'LupperArm'];

const L_rest = { LupperArm: [8, 0, -16], Lforearm: [-30, 0, 0] };
const L_out = { LupperArm: [10, 0, -72], Lforearm: [-18, 0, 0] };
const L_back = { LupperArm: [55, 0, -35], Lforearm: [-14, 0, 0] };
const L_front = { LupperArm: [-62, 0, -24], Lforearm: [-40, 0, 0] };
const L_tuck = { LupperArm: [-30, 0, -10], Lforearm: [-110, 0, 0] };

export const neutral = {
  pelvis: [6, -12, 0], spine: [6, 2, 0], chest: [5, 8, 0], neck: [0, 2, 0], head: [-8, 2, 0],
  Lclavicle: [0, 0, 0], Rclavicle: [0, 0, 0],
  RupperArm: [-20, 0, -10], Rforearm: [-40, 0, 0], Rhand: [0, 0, 0],
  ...L_rest, Lhand: [0, 0, 0],
  Rthigh: [-20, 0, -3], Rshin: [26, 0, 0], Rfoot: [-6, 0, 0], Rtoe: [0, 0, 0],
  Lthigh: [12, 0, 5], Lshin: [22, 0, 0], Lfoot: [-10, 0, 0], Ltoe: [0, 0, 0],
};
// Planted ankles (root space) in the idle stance: blade side (right) leads.
export const STANCE_FEET = { L: [-.19, -.16], R: [.15, .16] };
const key = (t, angles = {}, body = [0, 0, 0], ease = 'smooth') => ({ t, angles: { ...neutral, ...angles }, body, ease });

const lungeR = { Rthigh: [-50, 0, -4], Rshin: [54, 0, 0], Rfoot: [-10, 0, 0], Lthigh: [26, 0, 5], Lshin: [24, 0, 0], Lfoot: [18, 0, 0] };
const lungeL = { Lthigh: [-50, 0, 4], Lshin: [54, 0, 0], Lfoot: [-10, 0, 0], Rthigh: [26, 0, -5], Rshin: [24, 0, 0], Rfoot: [18, 0, 0] };
const deepR = { Rthigh: [-72, 0, -4], Rshin: [72, 0, 0], Rfoot: [-14, 0, 0], Lthigh: [40, 0, 6], Lshin: [16, 0, 0], Lfoot: [30, 0, 0] };
const tucked = { Lthigh: [-70, 0, 10], Rthigh: [-60, 0, -10], Lshin: [110, 0, 0], Rshin: [110, 0, 0] };

// after the horizontal backhand: right foot forward, body turned right, arm out to the right rear
const l1Hold = { pelvis: [10, 30, 0], spine: [6, 12, 0], chest: [4, 38, 0], neck: [-2, -18, 0], head: [-6, -18, 0], ...lungeR, ...L_back };
// after the rising cut: left foot forward, torso turned right and extended upward
// after the wide forehand sweep: right foot forward, body turned left, arm extended to the left front
const l2Hold = { pelvis: [12, -30, 0], spine: [8, -10, 0], chest: [6, -38, 0], neck: [-2, 16, 0], head: [-6, 16, 0], ...lungeR, ...L_back };
const spinLean = { pelvis: [10, 0, -10], spine: [6, 0, -8], chest: [6, 0, -10], neck: [0, 0, 8], head: [-4, 0, 6], Rthigh: [-30, 0, -10], Rshin: [44, 0, 0], Lthigh: [-8, 0, 12], Lshin: [44, 0, 0], ...L_out };
const l3Hold = { pelvis: [14, -28, 0], spine: [8, -10, 0], chest: [8, -30, 0], neck: [-4, 16, 0], head: [-8, 16, 0], ...lungeR, ...L_back };
const charge = { pelvis: [8, 48, 0], spine: [4, 10, 0], chest: [4, 22, 0], neck: [0, -28, 0], head: [-6, -30, 0], Rthigh: [-30, 0, -3], Rshin: [44, 0, 0], Lthigh: [20, 0, 5], Lshin: [36, 0, 0], ...L_front };
const spinPose = { pelvis: [4, 0, -6], spine: [0, 0, -8], chest: [0, 0, -10], neck: [0, 0, 6], head: [0, 0, 4], Rthigh: [-20, 0, -8], Rshin: [30, 0, 0], Lthigh: [-10, 0, 10], Lshin: [34, 0, 0], ...L_out };
const airSpin = { pelvis: [10, 0, -10], spine: [6, 0, -8], chest: [4, 0, -8], neck: [0, 0, 6], ...tucked, ...L_out };
const lungeHold = { ...deepR, pelvis: [23, -10, 0], spine: [12, 0, 0], chest: [8, 8, 0], neck: [-8, -4, 0], head: [-12, -4, 0], ...L_back };
const landHold = { pelvis: [18, -10, 0], spine: [12, 0, 0], chest: [10, 0, 0], neck: [-6, 0, 0], head: [-12, 0, 0], ...lungeR, ...L_back };
const finisher = { pelvis: [10, -22, 0], spine: [6, -6, 0], chest: [6, -26, 0], neck: [0, 14, 0], head: [-4, 14, 0], ...lungeR, ...L_back };

export const moves = {
  // Sling cut: the arm folds tight across the chest and the blade is cocked back over the LEFT
  // shoulder, the upper body coils left. Using the upper body as the axis it unwinds ~115 deg:
  // the upper arm leads around, the folded forearm lags, then whips straight just before contact
  // (like a sling), and the extended blade carries on to the right rear where hit 2 begins.
  light1: {
    label: 'Sling cut', key: 'A', duration: .80, blend: .08, active: [.21, .40], impact: .275, lag: 0, buffer: [.30, .76], chain: 'light2', chainAt: .50,
    // torso turns first and BRAKES before contact (kinetic chain), then barely drifts
    // Whip / kinetic chain: the torso turns moderately (~100 deg over 0.12 s) and brakes before
    // contact; only the blade accelerates at the end. Little extra chest twist on top of the yaw,
    // so the torso never spins as fast as the blade.
    yaw: [[0, 0], [.12, -36], [.19, -42], [.31, 8], [.42, 18], [.55, 18], [.80, 0]], dyn: .3, whipHz: 28,
    steps: [['R', .17, .31], ['L', .50, .72]],
    root: [[0, 0], [.17, 0], [.31, .50], [.39, .58], [.80, .58]],
    keys: [
      key(0),
      key(.14, { pelvis: [4, -5, 0], spine: [-2, -4, 0], chest: [-4, -12, 0], neck: [0, 10, 0], head: [-4, 10, 0], ...L_back }, [0, .03, -.03], 'in'),
      key(.20, { pelvis: [4, -6, 0], spine: [-2, -4, 0], chest: [-4, -14, 0], neck: [0, 12, 0], head: [-4, 12, 0], Rthigh: [-28, 0, -3], Rshin: [40, 0, 0], ...L_back }, [.02, .03, -.04]),
      key(.27, { pelvis: [6, -3, 0], spine: [2, -2, 0], chest: [0, -6, 0], neck: [0, 4, 0], head: [-4, 4, 0], ...lungeR, ...L_out }, [0, -.08, .12], 'out'),
      key(.36, { ...l1Hold, pelvis: [6, 8, 0], spine: [2, 4, 0], chest: [0, 14, 0] }, [0, -.10, .13]),
      key(.52, { ...l1Hold, pelvis: [6, 8, 0], spine: [2, 4, 0], chest: [0, 14, 0] }, [0, -.09, .12]),
      key(.66, { pelvis: [6, 8, 0], chest: [4, 10, 0], Lthigh: [0, 0, 5], Lshin: [38, 0, 0] }, [0, -.05, .03]),
      key(.80),
    ],
  },
  // Wide forehand sweep. From where hit 1 ended (low left) the blade is drawn back low across
  // the front to the right rear (arm stays OUTSIDE the body), the body coils right, then the
  // extended arm sweeps right -> left at waist-chest height while the whole body turns ~75 deg.
  light2: {
    label: 'Wide sweep', key: 'A²', duration: .82, blend: .08, active: [.20, .38], impact: .27, buffer: [.30, .78], chain: 'light3', chainAt: .52,
    yaw: [[0, 0], [.10, 15], [.20, 30], [.34, -45], [.52, -45], [.82, 0]],
    steps: [['R', .14, .28], ['L', .52, .74]],
    root: [[0, 0], [.12, 0], [.30, .62], [.40, .72], [.82, .72]],
    keys: [
      key(0, l1Hold, [-.03, -.14, .10]),
      key(.12, { pelvis: [10, 18, 0], spine: [6, 8, 0], chest: [4, 22, 0], neck: [0, -10, 0], head: [-4, -10, 0], Rthigh: [-24, 0, -3], Rshin: [46, 0, 0], ...L_front }, [0, -.16, .02], 'in'),
      key(.20, { pelvis: [10, 26, 0], spine: [6, 10, 0], chest: [4, 30, 0], neck: [0, -14, 0], head: [-4, -14, 0], Rthigh: [-34, 0, -3], Rshin: [52, 0, 0], ...L_front }, [0, -.18, .04]),
      key(.28, { pelvis: [12, -18, 0], spine: [8, -6, 0], chest: [6, -22, 0], neck: [-2, 10, 0], head: [-6, 10, 0], ...lungeR, ...L_back }, [-.02, -.20, .14], 'out'),
      key(.36, l2Hold, [-.03, -.20, .15]),
      key(.52, l2Hold, [-.03, -.16, .12]),
      key(.68, { pelvis: [6, -6, 0], chest: [4, -4, 0], Lthigh: [-6, 0, 5], Lshin: [40, 0, 0] }, [0, -.06, .03]),
      key(.82),
    ],
  },
  light3: {
    label: 'Full-circle sweep', key: 'A³', duration: 1.05, blend: .07, active: [.16, .58], impact: .30, multi: .22, air: [.18, .48],
    yaw: [[0, 0], [.17, 0], [.47, 360], [1.05, 360]],
    root: [[0, 0], [.12, .05], [.48, 1.15], [.60, 1.45], [1.05, 1.50]],
    keys: [
      key(0, l2Hold, [.03, -.14, .10]),
      key(.14, { pelvis: [14, -20, 0], spine: [8, -6, 0], chest: [8, -10, 0], neck: [0, 10, 0], head: [-6, 10, 0], Rthigh: [-40, 0, -5], Rshin: [60, 0, 0], Lthigh: [10, 0, 8], Lshin: [50, 0, 0], ...L_out }, [0, -.20, 0], 'in'),
      key(.30, spinLean, [0, -.12, 0]),
      key(.46, spinLean, [0, -.14, 0]),
      key(.56, l3Hold, [0, -.16, .10], 'out'),
      key(.80, l3Hold, [0, -.15, .10]),
      key(1.05),
    ],
  },
  heavyCharge: {
    label: 'Gathering', key: 'K↓', duration: 1.20, blend: .14, loopHold: true,
    root: [[0, 0], [1.20, 0]],
    keys: [key(0), key(.45, charge, [0, -.12, -.04]), key(1.20, { ...charge, pelvis: [10, 54, 0], chest: [5, 26, 0] }, [0, -.15, -.05])],
  },
  heavy: {
    label: 'Cyclone', key: 'K↑', duration: 1.45, blend: .06, active: [.12, .96], impact: .20, multi: .16, air: [.10, .86],
    yaw: [[0, 0], [.10, 0], [.78, 720], [1.45, 720]],
    root: [[0, 0], [.10, 0], [.80, .70], [1.45, .70]],
    keys: [
      key(0, { ...charge, pelvis: [10, 54, 0], chest: [5, 26, 0] }, [0, -.15, -.05]),
      key(.10, { pelvis: [6, 10, 0], spine: [0, 0, -6], chest: [0, 0, -8], ...L_out, Rthigh: [-24, 0, -6], Rshin: [36, 0, 0], Lthigh: [-6, 0, 8], Lshin: [36, 0, 0] }, [0, -.08, 0]),
      key(.45, spinPose, [0, .06, 0]),
      key(.80, spinPose, [0, -.02, 0]),
      key(.92, finisher, [0, -.14, .08], 'out'),
      key(1.15, finisher, [0, -.13, .08]),
      key(1.45),
    ],
  },
  roll: {
    label: 'Dodge roll', key: 'SPACE', duration: .92, blend: .07, roll: true, air: [.08, .80], weaponSpace: 'chest',
    root: [[0, 0], [.12, .25], [.45, 2.5], [.72, 3.85], [.92, 4]],
    keys: [key(0),
      key(.12, { pelvis: [35, 0, 0], chest: [35, 0, 0], ...L_tuck, Lthigh: [-78, 0, 8], Rthigh: [-65, 0, -8], Lshin: [115, 0, 0], Rshin: [110, 0, 0] }, [0, -.25, 0]),
      key(.33, { pelvis: [145, 0, 0], spine: [42, 0, 0], chest: [34, 0, 0], neck: [38, 0, 0], head: [30, 0, 0], ...L_tuck, Lthigh: [-110, 0, 8], Rthigh: [-110, 0, -8], Lshin: [140, 0, 0], Rshin: [140, 0, 0] }, [0, -.06, 0]),
      key(.55, { pelvis: [275, 0, 0], spine: [42, 0, 0], chest: [34, 0, 0], neck: [38, 0, 0], head: [30, 0, 0], ...L_tuck, Lthigh: [-105, 0, 8], Rthigh: [-105, 0, -8], Lshin: [135, 0, 0], Rshin: [135, 0, 0] }, [0, -.10, 0]),
      key(.76, { pelvis: [350, 0, 0], chest: [25, 0, 0], Lthigh: [-48, 0, 8], Rthigh: [-30, 0, -8], Lshin: [70, 0, 0], Rshin: [55, 0, 0] }, [0, -.14, 0]),
      key(.92, { pelvis: [366, -12, 0] })],
    // chest space: blade folded back along the body
  },
  backstep: {
    label: 'Backstep', key: 'SPACE', duration: .62, blend: .08, air: [.12, .44], land: .44,
    root: [[0, 0], [.08, -.05], [.33, -1.60], [.48, -1.75], [.62, -1.75]],
    keys: [key(0),
      key(.10, { pelvis: [16, -12, 0], chest: [-4, 8, 0], Rthigh: [-34, 0, -5], Lthigh: [-10, 0, 5], Rshin: [48, 0, 0], Lshin: [40, 0, 0], ...L_front }, [0, -.12, 0]),
      key(.28, { pelvis: [-4, -12, 0], Rthigh: [8, 0, -5], Lthigh: [22, 0, 5], Rshin: [40, 0, 0], Lshin: [55, 0, 0], ...L_out }, [0, .14, -.02]),
      key(.46, { pelvis: [16, -12, 0], Rthigh: [-36, 0, -5], Lthigh: [-12, 0, 5], Rshin: [58, 0, 0], Lshin: [50, 0, 0], ...L_out }, [0, -.15, 0]),
      key(.62)],
  },
  // Jump: crouch (anticipation) -> full extension at take-off -> ballistic arc (rise slows,
  // fall speeds up; 'arc' adds the parabola) with tucked legs -> legs reach -> landing squash.
  jump: {
    label: 'Jump', key: 'F', duration: 1.15, blend: .08, air: [.18, .90], land: .90, arc: [.18, .90, 1.05], rootEase: 'linear',
    root: [[0, 0], [.18, 0], [.90, .90], [1.15, .95]],
    keys: [key(0),
      key(.12, { pelvis: [18, -10, 0], spine: [8, 0, 0], chest: [12, 6, 0], neck: [-6, 0, 0], head: [-8, 0, 0], Lthigh: [-45, 0, 7], Rthigh: [-55, 0, -7], Lshin: [80, 0, 0], Rshin: [85, 0, 0], Lfoot: [-15, 0, 0], Rfoot: [-15, 0, 0], ...L_back }, [0, -.24, 0], 'in'),
      key(.18, { pelvis: [-4, -10, 0], spine: [-4, 0, 0], chest: [-6, 6, 0], Lthigh: [-10, 0, 7], Rthigh: [5, 0, -7], Lshin: [15, 0, 0], Rshin: [10, 0, 0], Lfoot: [35, 0, 0], Rfoot: [40, 0, 0], ...L_front }, [0, .02, 0], 'out'),
      key(.40, { pelvis: [-2, -10, 0], Rthigh: [-55, 0, -7], Rshin: [95, 0, 0], Lthigh: [-20, 0, 7], Lshin: [80, 0, 0], Lfoot: [20, 0, 0], Rfoot: [10, 0, 0], ...L_out }, [0, 0, 0]),
      key(.60, { pelvis: [4, -10, 0], Rthigh: [-60, 0, -7], Rshin: [100, 0, 0], Lthigh: [-30, 0, 7], Lshin: [95, 0, 0], ...L_out }, [0, 0, 0]),
      key(.82, { pelvis: [6, -10, 0], Rthigh: [-30, 0, -7], Rshin: [35, 0, 0], Lthigh: [-10, 0, 7], Lshin: [25, 0, 0], Rfoot: [10, 0, 0], Lfoot: [10, 0, 0], ...L_out }, [0, 0, 0]),
      key(.94, { pelvis: [16, -10, 0], spine: [10, 0, 0], chest: [8, 6, 0], ...lungeR, ...L_back }, [0, -.26, 0], 'out'),
      key(1.15)],
  },
  flurry: {
    label: 'Blade storm', key: 'F → J', duration: 1.55, blend: .06, active: [.10, 1.18], multi: .14, impact: 1.20, slam: true, air: [0, 1.18], land: 1.20,
    yaw: [[0, 0], [.12, 0], [.95, 1080], [1.55, 1080]],
    root: [[0, 0], [1.20, .8], [1.55, .8]],
    keys: [
      key(0, { pelvis: [-4, 0, 0], Lthigh: [-50, 0, 7], Rthigh: [-25, 0, -7], Lshin: [80, 0, 0], Rshin: [90, 0, 0], ...L_out }, [0, .95, 0]),
      key(.20, airSpin, [0, 1.25, 0]),
      key(.85, airSpin, [0, 1.05, 0]),
      key(1.10, { pelvis: [14, 0, 0], chest: [10, 0, 0], Lthigh: [-40, 0, 7], Rthigh: [-20, 0, -7], Lshin: [60, 0, 0], Rshin: [70, 0, 0], ...L_back }, [0, .45, .05], 'in'),
      key(1.20, landHold, [0, -.30, .10], 'out'),
      key(1.35, landHold, [0, -.28, .10]),
      key(1.55),
    ],
  },
  hit: {
    label: 'Hit reaction', key: 'H', duration: .62, blend: .045, steps: [['L', .04, .22]],
    root: [[0, 0], [.14, -.30], [.62, -.36]],
    keys: [key(0),
      key(.09, { pelvis: [-10, -16, 3], spine: [-8, -8, 3], chest: [-18, 4, 8], neck: [-12, 6, 4], head: [-14, 8, 4], Lthigh: [26, 0, 6], Lshin: [30, 0, 0], ...L_out }, [0, -.04, -.08], 'out'),
      key(.26, { pelvis: [12, -10, 0], chest: [12, 6, 0], head: [-2, 0, 0] }, [0, -.12, 0]),
      key(.62)],
  },
  stagger: {
    label: 'Stagger', key: 'H×2', duration: 1.50, blend: .06, steps: [['L', .05, .22], ['R', .28, .50], ['L', .95, 1.20]],
    root: [[0, 0], [.18, -.30], [.50, -.90], [1.50, -1.0]],
    keys: [key(0),
      key(.15, { pelvis: [-16, -20, 8], spine: [-10, 8, 4], chest: [-24, 12, 12], neck: [-14, 0, 0], head: [-14, 0, 0], LupperArm: [20, 0, -55], Lforearm: [-30, 0, 0], Lthigh: [28, 0, 6], Lshin: [30, 0, 0] }, [.08, -.06, -.06]),
      key(.48, { pelvis: [24, -2, -8], spine: [16, -8, -4], chest: [20, -12, -6], neck: [10, 0, 0], head: [10, 0, 0], LupperArm: [-30, 0, -30], Lforearm: [-50, 0, 0], Rthigh: [-58, 0, -6], Rshin: [90, 0, 0], Lthigh: [8, 0, 8], Lshin: [55, 0, 0] }, [0, -.26, .04]),
      key(.95, { pelvis: [18, -4, 0], spine: [10, 0, 0], chest: [12, 6, 0], neck: [4, 0, 0], head: [2, 0, 0], LupperArm: [-30, 0, -20], Lforearm: [-70, 0, 0] }, [0, -.16, 0]),
      key(1.50)],
  },
  parry: {
    label: 'Blade deflect', key: 'L', duration: .60, blend: .04, active: [.05, .23], impact: .10, parry: true,
    root: [[0, 0], [.10, .10], [.60, .10]],
    keys: [key(0),
      key(.05, { pelvis: [8, 10, 0], spine: [4, 4, 0], chest: [-2, 6, 0], neck: [0, -6, 0], head: [-4, -6, 0], ...L_front }, [.02, -.09, 0], 'out'),
      key(.23, { pelvis: [8, 12, 0], spine: [4, 4, 0], chest: [-2, 8, 0], neck: [0, -6, 0], head: [-4, -6, 0], ...L_front }, [.02, -.10, .01]),
      key(.45, { pelvis: [6, -4, 0] }, [0, -.03, 0]),
      key(.60)],
  },
};

// ---------- Arm-blade swing keys ----------
// [time, upper-arm dir u, forearm dir f, blade dir d], root space (+Z forward, +X right).
// Keys follow real cutting mechanics: the swing runs on a tilted plane, the upper arm leads,
// the forearm follows, the blade lags cocked behind the forearm (wrist ~80-90 deg) until the
// wrist releases at contact, then follows through past the target. Consecutive keys stay
// under ~90 degrees apart. The forearm/blade are also sampled with a small delay in anim.js.
export const IDLE_GRIP = [[.25, -.92, .30], [.05, -.35, .94], [-.15, .30, .94]];
const IDLE_UD = [[.25, -.92, .30], [-.15, .30, .94]];
const A = (t, u, f, d) => [t, u, f, d], idleA = t => [t, ...IDLE_GRIP];
const L1_END = [[.85, -.05, .52], [.90, -.05, .42], [.90, -.05, .42]];
const SWEEP_END = [[.85, -.30, -.40], [.70, -.30, -.65], [.70, -.30, -.65]];
const L2_END = [[-.55, -.12, .83], [-.90, -.08, .43], [-.90, -.08, .43]];
const CHARGE = [[.38, -.66, -.65], [.35, -.63, -.69], [.40, -.33, -.86]];
const OUT = [[.90, -.40, .15], [.90, -.42, .12], [.88, -.45, .10]];
const ARM_GUIDES = {
  // whip: fold across the chest with the blade back over the left shoulder; the upper arm swings
  // forward first and then slows, while the blade pivots on the elbow around the OUTSIDE (left)
  // and accelerates past it — the hand moves a small arc, the tip a huge fast one (~2500 deg/s
  // at contact). Upper arm turns ~100 deg in total, the blade ~190 deg.
  light1: [idleA(0),
    A(.07, [-.10, -.20, .97], [-.50, .40, .77], [-.50, .40, .77]),
    A(.12, [-.66, .23, .71], [-.80, .30, -.52], [-.80, .30, -.52]),      // folded, blade over the left shoulder
    A(.18, [-.68, .25, .69], [-.78, .32, -.54], [-.78, .32, -.54]),
    A(.23, [-.10, .10, .99], [-.90, .25, -.35], [-.90, .25, -.35]),      // torso + upper arm lead, blade still trails folded
    A(.25, [.10, .05, .99], [-.95, .10, .25], [-.95, .10, .25]),         // upper arm braking, blade swinging out on the left
    A(.262, [.18, 0, .98], [-.45, 0, .89], [-.45, 0, .89]),              // whip: blade accelerating past the arm
    A(.274, [.25, -.02, .97], [.30, -.02, .95], [.30, -.02, .95]),       // contact: arm straight, tip at peak speed
    A(.30, [.55, -.04, .83], [.60, -.05, .80], [.60, -.05, .80]),        // arm + blade in one line, decelerating
    A(.34, [.80, -.05, .60], [.85, -.05, .52], [.85, -.05, .52]),
    A(.36, L1_END[0], L1_END[1], L1_END[2]),                              // right rear = where hit 2 starts
    A(.52, [.85, -.07, .52], [.90, -.07, .43], [.90, -.07, .43]),
    A(.66, [.60, -.60, .50], [.55, -.45, .70], [.55, -.45, .70]),
    idleA(.80)],
  // wide forehand sweep: elbow and wrist stay in front of / outside the chest the whole way
  light2: [A(0, L1_END[0], L1_END[1], L1_END[2]),
    A(.10, [.84, -.30, -.44], [.73, -.20, -.64], [.73, -.20, -.64]),      // already at the right rear: deepen the coil
    A(.20, [.85, -.25, -.45], [.75, -.15, -.64], [.75, -.15, -.64]),
    A(.23, [.92, -.15, .35], [.95, -.08, .30], [.95, -.08, .30]),
    A(.26, [.35, -.12, .93], [.10, -.08, .99], [.10, -.08, .99]),        // contact in front
    A(.29, [-.30, -.12, .95], [-.62, -.08, .78], [-.62, -.08, .78]),
    A(.34, L2_END[0], L2_END[1], L2_END[2]),                              // follow-through to the left front
    A(.50, [-.55, -.14, .82], [-.88, -.10, .46], [-.88, -.10, .46]),
    A(.64, [.10, -.75, .65], [-.30, -.40, .86], [-.30, -.40, .86]),
    idleA(.82)],
  // full-circle sweep: arm extended outward-down while the whole body spins once, closing cut across the front
  light3: [A(0, L2_END[0], L2_END[1], L2_END[2]),
    A(.07, [.30, -.80, .52], [.10, -.70, .71], [.10, -.70, .71]),
    A(.14, [.75, -.55, -.35], [.75, -.45, -.48], [.75, -.45, -.48]),
    A(.18, [.92, -.28, .25], [.92, -.25, .30], [.92, -.25, .30]),
    A(.46, [.92, -.28, .25], [.92, -.25, .30], [.92, -.25, .30]),
    A(.52, [.50, -.30, .81], [.20, -.30, .93], [.20, -.30, .93]),
    A(.56, [.10, -.35, .93], [-.45, -.35, .82], [-.45, -.35, .82]),
    A(.80, [.10, -.37, .92], [-.45, -.37, .81], [-.45, -.37, .81]),
    idleA(1.05)],
  heavyCharge: [idleA(0),
    A(.12, [.35, -.92, .10], [.30, -.60, .74], [.30, -.05, .95]),
    A(.25, [.45, -.85, -.15], [.55, -.75, .35], [.65, -.70, .10]),
    A(.60, [.38, -.68, -.63], [.35, -.65, -.67], [.40, -.35, -.85]),
    A(1.20, CHARGE[0], CHARGE[1], CHARGE[2])],
  // cyclone: arm angled DOWN-out (not at shoulder height) while spinning, then a diagonal closing cut
  heavy: [A(0, CHARGE[0], CHARGE[1], CHARGE[2]),
    A(.10, OUT[0], OUT[1], OUT[2]),
    A(.80, OUT[0], OUT[1], OUT[2]),
    A(.86, [.70, -.30, .65], [.50, -.30, .81], [.30, -.25, .92]),
    A(.92, [.30, -.35, .89], [-.10, -.50, .86], [-.55, -.55, .63]),
    A(1.15, [.30, -.37, .88], [-.10, -.52, .85], [-.55, -.57, .61]),
    idleA(1.45)],
  // chest space: arm hanging back along the body, blade trailing
  roll: [A(0, [.25, -.95, .15], [.10, -.40, -.91], [.05, -.30, -.95]), A(.92, [.25, -.95, .15], [.10, -.40, -.91], [.05, -.30, -.95])],
  backstep: [idleA(0), A(.10, [.30, -.90, .30], [.10, -.45, .89], [-.10, .10, .99]), A(.28, [.40, -.80, .45], [.25, -.20, .95], [.05, .35, .94]), A(.46, [.30, -.90, .30], [.10, -.45, .89], [-.10, .10, .99]), idleA(.62)],
  jump: [idleA(0), A(.12, [.30, -.90, .30], [.10, -.50, .86], [0, -.10, 1]), A(.40, [.50, -.65, .57], [.35, -.20, .92], [.10, .25, .96]), A(.80, [.50, -.65, .57], [.35, -.20, .92], [.10, .25, .96]), A(.94, [.30, -.88, .36], [.10, -.55, .83], [-.05, -.25, .97]), idleA(1.15)],
  // blade storm: arm out and down while spinning, then cock overhead and drive into the ground
  flurry: [A(0, [.50, -.65, .57], [.35, -.20, .92], [.10, .25, .96]),
    A(.12, OUT[0], OUT[1], [.80, -.58, .10]),
    A(.95, OUT[0], OUT[1], [.80, -.58, .10]),
    A(.985, [.85, -.10, .45], [.75, 0, .62], [.70, .20, .65]),
    A(1.02, [.75, .20, .60], [.55, .55, .62], [.30, .85, .40]),
    A(1.10, [.40, .60, .70], [.20, .95, .25], [-.10, .60, -.79]),
    A(1.15, [.30, .20, .93], [.10, .40, .91], [0, .75, .66]),
    A(1.175, [.25, 0, .97], [.08, -.10, .99], [0, .10, 1]),
    A(1.20, [.20, -.25, .95], [.05, -.55, .83], [0, -.72, .69]),
    A(1.35, [.20, -.27, .94], [.05, -.57, .82], [0, -.72, .69]),
    idleA(1.55)],
  hit: [idleA(0), A(.045, [.55, -.65, .10], [.82, -.41, .41], [.71, .39, .58]), A(.09, [.75, -.30, -.55], [.70, .10, -.70], [.75, .35, -.55]), A(.17, [.60, -.60, -.15], [.93, -.31, .21], [.77, .46, .46]), A(.26, [.40, -.85, .30], [.20, -.40, .89], [0, .10, 1]), idleA(.62)],
  stagger: [idleA(0), A(.08, [.55, -.65, .10], [.82, -.41, .41], [.71, .39, .58]), A(.15, [.80, -.35, -.45], [.80, .10, -.55], [.85, .25, -.45]), A(.30, [.70, -.60, -.10], [.75, -.30, .10], [.85, -.10, .30]), A(.48, [.55, -.80, .20], [.50, -.60, .62], [.40, -.40, .82]), A(.95, [.35, -.88, .30], [.15, -.45, .88], [-.05, -.05, 1]), idleA(1.50)],
  parry: [idleA(0), A(.03, [.20, -.60, .77], [.05, -.05, 1], [-.10, .50, .86]), A(.06, [.20, -.40, .89], [0, .20, .98], [-.30, .95, .10]), A(.23, [.20, -.40, .89], [0, .20, .98], [-.30, .95, .12]), A(.45, [.30, -.65, .70], [.10, -.15, .98], [-.05, .40, .92]), idleA(.60)],
};
// Forearm and blade are one rigid line, so the arm extends through the cut: from contact
// through the follow-through the upper arm is pulled toward the blade line (nearly straight
// arm, blade continuing it), the way a prosthetic blade is swung.
const norm = v => { const l = Math.hypot(...v); return v.map(c => c / l); };
for (const [name, list] of Object.entries(ARM_GUIDES)) {
  const mv = moves[name], from = mv.impact ?? Infinity, to = (mv.active?.[1] ?? -1) + .2;
  moves[name].weapon = list.map(([t, u, f, d]) => (t >= from - .001 && t <= to && !mv.multi) ? [t, norm(u.map((c, i) => c + 1.5 * norm(d)[i])), f, d] : [t, u, f, d]);
}

// ---------- Skills (QWER / dash). Keys carry their own swing data ([t, u, f, d]). ----------
const S = (t, u, d) => [t, u, d, d];
Object.assign(moves, {
  wave: {
    label: 'Laser wave', key: 'W', duration: .85, blend: .07, active: [.24, .36], impact: .30, fx: [[.30, 'wave']],
    steps: [['R', .14, .28]],
    root: [[0, 0], [.14, 0], [.30, .30], [.85, .30]],
    keys: [key(0),
      key(.12, { pelvis: [12, -34, 0], spine: [8, -10, 0], chest: [6, -34, 0], neck: [-2, 16, 0], head: [-6, 16, 0], Rthigh: [-26, 0, -3], Rshin: [46, 0, 0], ...L_front }, [0, -.16, -.02], 'in'),
      key(.20, { pelvis: [12, -40, 0], spine: [8, -12, 0], chest: [6, -40, 0], neck: [-2, 18, 0], head: [-6, 18, 0], Rthigh: [-30, 0, -3], Rshin: [50, 0, 0], ...L_front }, [0, -.18, 0]),
      key(.30, { pelvis: [2, 30, 0], spine: [-4, 10, 0], chest: [-8, 34, 0], neck: [2, -14, 0], head: [0, -14, 0], ...lungeR, ...L_back }, [.02, -.06, .10], 'out'),
      key(.55, { pelvis: [2, 34, 0], spine: [-4, 10, 0], chest: [-8, 38, 0], neck: [2, -16, 0], head: [0, -16, 0], ...lungeR, ...L_back }, [.02, -.07, .10]),
      key(.85)],
    weapon: [S(0, ...IDLE_UD), S(.10, [.20, -.60, .77], [-.40, -.55, .73]), S(.18, [0, -.55, .83], [-.75, -.45, -.48]), S(.22, [.05, -.45, .89], [-.70, -.25, .67]),
      S(.26, [.35, -.15, .92], [-.10, .05, .99]), S(.30, [.70, .10, .70], [.70, .25, .67]), S(.36, [.85, .35, .38], [.70, .60, .38]), S(.55, [.84, .36, .40], [.70, .58, .40]),
      S(.70, [.45, -.60, .66], [.30, -.10, .95]), S(.85, ...IDLE_UD)],
  },
  counter: {
    label: 'Counter', key: 'E', duration: .95, blend: .04, active: [.31, .44], impact: .36, fx: [[.06, 'parry']], parryWindow: [.04, .26],
    steps: [['R', .28, .38]],
    root: [[0, 0], [.06, -.08], [.28, -.10], [.38, .40], [.95, .40]],
    keys: [key(0),
      key(.06, { pelvis: [8, 12, 0], spine: [4, 4, 0], chest: [-2, 10, 0], neck: [0, -8, 0], head: [-4, -8, 0], ...L_front }, [0, -.10, -.03], 'out'),
      key(.26, { pelvis: [10, 4, 0], spine: [4, 0, 0], chest: [0, -10, 0], neck: [0, 4, 0], head: [-4, 4, 0], ...L_front }, [0, -.13, -.03]),
      key(.36, { pelvis: [10, 34, 0], spine: [6, 10, 0], chest: [4, 36, 0], neck: [-2, -16, 0], head: [-6, -16, 0], ...lungeR, ...L_back }, [0, -.14, .10], 'out'),
      key(.62, { pelvis: [10, 38, 0], spine: [6, 12, 0], chest: [4, 40, 0], neck: [-2, -18, 0], head: [-6, -18, 0], ...lungeR, ...L_back }, [0, -.13, .10]),
      key(.95)],
    weapon: [S(0, ...IDLE_UD), S(.05, [.20, -.40, .89], [-.30, .95, .10]), S(.25, [.20, -.40, .89], [-.30, .95, .12]), S(.30, [.10, -.20, .97], [-.70, .40, .60]),
      S(.33, [.40, -.15, .90], [0, .05, 1]), S(.36, [.80, -.20, .55], [.85, -.20, .48]), S(.44, SWEEP_END[0], SWEEP_END[2]), S(.62, [.85, -.32, -.40], [.70, -.32, -.64]),
      S(.80, [.70, -.55, .05], [.80, -.45, .10]), S(.88, [.45, -.80, .40], [.40, -.45, .80]), S(.95, ...IDLE_UD)],
  },
  dash: {
    label: 'Dash', key: 'SPACE', duration: .42, blend: .04, air: [.04, .30], land: .30, fx: [[.02, 'dash']],
    root: [[0, 0], [.04, .1], [.26, 3.4], [.34, 3.6], [.42, 3.6]],
    keys: [key(0),
      key(.05, { pelvis: [34, -10, 0], spine: [12, 0, 0], chest: [10, 6, 0], neck: [-14, 0, 0], head: [-16, 0, 0], Rthigh: [-50, 0, -5], Rshin: [80, 0, 0], Lthigh: [30, 0, 5], Lshin: [70, 0, 0], ...L_back }, [0, -.20, .05], 'out'),
      key(.26, { pelvis: [34, -10, 0], spine: [12, 0, 0], chest: [10, 6, 0], neck: [-14, 0, 0], head: [-16, 0, 0], Rthigh: [-50, 0, -5], Rshin: [80, 0, 0], Lthigh: [30, 0, 5], Lshin: [70, 0, 0], ...L_back }, [0, -.22, .05]),
      key(.34, { pelvis: [14, -10, 0], chest: [4, 6, 0], ...lungeR, ...L_out }, [0, -.16, .03], 'out'),
      key(.42)],
    weapon: [S(0, ...IDLE_UD), S(.025, [.28, -.85, .10], [.25, -.70, .67]), S(.05, [.30, -.85, -.30], [.45, -.85, -.25]), S(.26, [.30, -.85, -.30], [.45, -.85, -.25]), S(.34, [.28, -.88, .10], [.25, -.70, .67]), S(.42, ...IDLE_UD)],
  },
});

// ---------- Slot-part actives (기획서/스킬_슬롯_카탈로그.md 8절). Move name = part id. ----------
// Left-arm tools lead with the left shoulder (pelvis turned right, head counter-turned) and park
// the blade low behind the right hip; body/head parts keep the blade out of the torso.
const IDLE_S = t => S(t, ...IDLE_UD);
const MID = [[.28, -.85, .10], [.25, -.70, .67]], BACK = [[.30, -.85, -.30], [.45, -.85, -.25]];
const LOWOUT = [[.45, -.80, .40], [.30, -.40, .86]], DOWNF = [[.30, -.85, .30], [.10, -.40, .91]];
const park = (end, t = .08) => [IDLE_S(0), S(t * .5, ...MID), S(t, ...BACK), S(end - .16, ...BACK), S(end - .08, ...MID), IDLE_S(end)];
const LL = { pelvis: [8, 26, 0], spine: [4, 8, 0], chest: [2, 18, 0], neck: [0, -20, 0], head: [-4, -22, 0], ...lungeL };
const L_aim = { LupperArm: [-86, 0, -10], Lforearm: [-8, 0, 0] };
const wide = { Rthigh: [-30, 0, 12], Rshin: [48, 0, 0], Lthigh: [14, 0, -12], Lshin: [44, 0, 0] };
const crouchJ = { pelvis: [18, -10, 0], spine: [8, 0, 0], chest: [12, 6, 0], neck: [-6, 0, 0], head: [-8, 0, 0], Lthigh: [-45, 0, 7], Rthigh: [-55, 0, -7], Lshin: [80, 0, 0], Rshin: [85, 0, 0], Lfoot: [-15, 0, 0], Rfoot: [-15, 0, 0] };
const shake = s => ({ pelvis: [14, -10, 6 * s], spine: [8, 0, 12 * s], chest: [8, 4, 16 * s], neck: [0, 0, -14 * s], head: [-4, 0, -12 * s], ...L_out });
const hunch = { pelvis: [30, -10, 0], spine: [20, 0, 0], chest: [22, 4, 0], neck: [-16, 0, 0], head: [-18, 0, 0], LupperArm: [20, 0, -50], Lforearm: [-20, 0, 0], Rthigh: [-40, 0, -3], Rshin: [60, 0, 0], Lthigh: [10, 0, 5], Lshin: [44, 0, 0] };
const thrustHold = { pelvis: [10, -8, 0], spine: [6, -4, 0], chest: [4, -12, 0], neck: [-2, 8, 0], head: [-6, 8, 0], ...lungeR, ...L_back };
const dashCoil = { pelvis: [30, 24, 0], spine: [12, 6, 0], chest: [10, 26, 0], neck: [-14, -16, 0], head: [-16, -16, 0], Rthigh: [-50, 0, -5], Rshin: [80, 0, 0], Lthigh: [30, 0, 5], Lshin: [70, 0, 0], ...L_back };
const reach = { pelvis: [14, -6, 0], spine: [10, -2, 0], chest: [8, -8, 0], neck: [-6, 4, 0], head: [-10, 4, 0], ...lungeR, ...L_back };
const crushHold = { ...deepR, pelvis: [24, -6, 0], spine: [14, 0, 0], chest: [12, -4, 0], neck: [-8, 2, 0], head: [-12, 2, 0], ...L_back };
const pullBack = { pelvis: [-4, -10, 0], spine: [-6, -4, 0], chest: [-10, -6, 0], neck: [4, 6, 0], head: [2, 6, 0], Rthigh: [-10, 0, -3], Rshin: [30, 0, 0], Lthigh: [20, 0, 5], Lshin: [40, 0, 0], ...L_out };
const braced = { ...LL, pelvis: [-2, 22, 0], spine: [-6, 6, 0], chest: [-8, 14, 0], neck: [4, -18, 0], head: [0, -20, 0], ...L_aim, Lthigh: [-30, 0, 4], Lshin: [30, 0, 0], Rthigh: [24, 0, -5], Rshin: [50, 0, 0] };
const palmHold = { ...LL, pelvis: [12, 30, 0], spine: [6, 10, 0], chest: [4, 22, 0], neck: [-2, -24, 0], head: [-6, -24, 0], LupperArm: [-80, 0, -6], Lforearm: [-4, 0, 0] };
const mineHold = { pelvis: [34, 16, 0], spine: [16, 6, 0], chest: [12, 10, 0], neck: [-14, -10, 0], head: [-16, -10, 0], Lthigh: [-70, 0, 4], Lshin: [80, 0, 0], Lfoot: [-12, 0, 0], Rthigh: [30, 0, -5], Rshin: [40, 0, 0], Rfoot: [20, 0, 0], LupperArm: [-55, 0, -20], Lforearm: [-15, 0, 0] };
const anchorHold = { pelvis: [20, -10, 0], spine: [10, 0, 0], chest: [8, 0, 0], neck: [-6, 0, 0], head: [-10, 0, 0], Rthigh: [-55, 0, 18], Lthigh: [-30, 0, -18], Rshin: [85, 0, 0], Lshin: [80, 0, 0], Rfoot: [-10, 0, 0], Lfoot: [-10, 0, 0], ...L_out };
const chargeHold = { pelvis: [30, 20, 0], spine: [14, 8, 0], chest: [12, 20, 0], neck: [-14, -18, 0], head: [-16, -20, 0], Rthigh: [-50, 0, -5], Rshin: [80, 0, 0], Lthigh: [30, 0, 5], Lshin: [70, 0, 0], LupperArm: [-30, 0, -10], Lforearm: [-110, 0, 0] };
const openChest = { pelvis: [-2, -10, 0], spine: [-10, 0, 0], chest: [-16, 6, 0], neck: [4, 0, 0], head: [-2, 0, 0], LupperArm: [0, 0, -80], Lforearm: [-10, 0, 0] };
const curl = { pelvis: [20, -10, 0], spine: [16, 0, 0], chest: [18, 6, 0], neck: [-2, 0, 0], head: [-4, 0, 0], ...L_tuck, Rthigh: [-40, 0, -3], Rshin: [64, 0, 0], Lthigh: [0, 0, 5], Lshin: [50, 0, 0] };
const burst = { pelvis: [-6, -10, 0], spine: [-12, 0, 0], chest: [-18, 6, 0], neck: [-10, 0, 0], head: [-14, 0, 0], LupperArm: [-20, 0, -95], Lforearm: [-5, 0, 0], ...lungeR };
const gather = { pelvis: [16, -10, 0], spine: [10, 0, 0], chest: [10, 4, 0], neck: [4, 0, 0], head: [4, 0, 0], LupperArm: [-30, 0, -30], Lforearm: [-90, 0, 0], Rthigh: [-36, 0, -3], Rshin: [60, 0, 0], Lthigh: [4, 0, 5], Lshin: [50, 0, 0] };
const skyward = { pelvis: [0, -10, 0], spine: [-12, 0, 0], chest: [-20, 4, 0], neck: [-16, 0, 0], head: [-20, 0, 0], LupperArm: [-10, 0, -70], Lforearm: [-10, 0, 0] };
const headThrust = { pelvis: [14, -10, 0], spine: [10, 0, 0], chest: [12, 6, 0], neck: [22, 0, 0], head: [10, 0, 0], ...L_back };
const screamOut = { pelvis: [16, -10, 0], spine: [14, 0, 0], chest: [20, 6, 0], neck: [24, 0, 0], head: [14, 0, 0], LupperArm: [45, 0, -30], Lforearm: [-10, 0, 0], ...lungeR };
const tossHold = { pelvis: [10, 20, 0], spine: [6, 8, 0], chest: [6, 18, 0], neck: [-2, -14, 0], head: [-6, -14, 0], LupperArm: [-120, 0, -10], Lforearm: [-5, 0, 0] };
const sawPose = { pelvis: [8, -10, 0], spine: [2, -4, 0], chest: [0, -14, 0], neck: [0, 8, 0], head: [-4, 8, 0], LupperArm: [-70, 0, -5], Lforearm: [-70, 0, 0], Rthigh: [-26, 0, -3], Rshin: [40, 0, 0] };
const guardPose = { pelvis: [12, 14, 0], spine: [6, 4, 0], chest: [4, 8, 0], neck: [-2, -8, 0], head: [-6, -8, 0], LupperArm: [-72, 0, -38], Lforearm: [-95, 0, 0], ...wide };
const pointPose = { ...LL, LupperArm: [-95, 0, -6], Lforearm: [-2, 0, 0] };
// the light2 cut from the right rear to the left front, relative times
const SWEEP_CUT = [S(0, [.85, -.25, -.45], [.75, -.15, -.64]), S(.04, [.92, -.15, .35], [.95, -.08, .30]), S(.07, [.35, -.12, .93], [.10, -.08, .99]), S(.10, [-.30, -.12, .95], [-.62, -.08, .78]), S(.15, L2_END[0], L2_END[2])];
const at = (t0, list) => list.map(([t, u, f, d]) => [t0 + t, u, f, d]);

Object.assign(moves, {
  // ---- 무기 (오른팔 칼날) ----
  harpoon: {
    label: 'Ejector blade', key: '사출 칼날', duration: 1.0, blend: .07, active: [.24, .68], impact: .27, multi: .34, fx: [[.25, 'harpoonOut'], [.60, 'harpoonIn']],
    steps: [['R', .14, .28]], root: [[0, 0], [.14, 0], [.28, .45], [1.0, .45]],
    keys: [key(0),
      key(.12, { pelvis: [10, 30, 0], spine: [4, 10, 0], chest: [2, 30, 0], neck: [0, -16, 0], head: [-4, -16, 0], Rthigh: [-26, 0, -3], Rshin: [46, 0, 0], ...L_front }, [0, -.14, -.04], 'in'),
      key(.27, thrustHold, [0, -.12, .14], 'out'), key(.55, thrustHold, [0, -.12, .14]),
      key(.64, { pelvis: [8, 14, 0], chest: [2, 18, 0], neck: [0, -8, 0], head: [-4, -8, 0], ...lungeR, ...L_back }, [0, -.08, .04], 'out'),
      key(.80, { pelvis: [6, 8, 0], chest: [4, 10, 0] }, [0, -.05, .02]), key(1.0)],
    weapon: [IDLE_S(0), S(.12, [.45, -.55, -.70], [.12, -.05, .99]), S(.20, [.50, -.50, -.70], [.12, -.05, .99]), S(.225, [.55, -.45, .10], [.15, -.05, .99]), S(.25, [.30, -.10, .95], [.18, -.05, .98]),
      S(.28, [.15, -.05, .99], [.10, -.05, .99]), S(.56, [.15, -.05, .99], [.10, -.05, .99]), S(.60, [.55, -.30, .40], [.20, -.05, .98]), S(.64, [.50, -.45, -.40], [.25, -.10, .96]),
      S(.80, [.40, -.80, .20], [.05, .10, .99]), IDLE_S(1.0)],
  },
  heatcut: {
    label: 'Heat cut', key: '열절단', duration: .92, blend: .05, active: [.08, .47], impact: .40, multi: .2, air: [.06, .38], land: .38, fx: [[.06, 'heatTrail'], [.40, 'heatEnd']],
    yaw: [[0, 0], [.32, 0], [.44, -30], [.62, -30], [.92, 0]],
    root: [[0, 0], [.06, .1], [.30, 5.6], [.38, 6.0], [.92, 6.0]],
    keys: [key(0), key(.06, dashCoil, [0, -.22, .05], 'out'), key(.30, dashCoil, [0, -.22, .05]), key(.35, { ...dashCoil, pelvis: [14, 12, 0], spine: [8, 4, 0], chest: [6, 14, 0] }, [0, -.20, .08]),
      key(.40, l2Hold, [-.03, -.20, .15], 'out'), key(.62, l2Hold, [-.03, -.16, .12]),
      key(.78, { pelvis: [6, -6, 0], chest: [4, -4, 0], Lthigh: [-6, 0, 5], Lshin: [40, 0, 0] }, [0, -.06, .03]), key(.92)],
    weapon: [IDLE_S(0), S(.03, ...MID), S(.045, [.60, -.60, -.10], [.60, -.55, .10]), S(.06, SWEEP_END[0], SWEEP_END[2]), S(.33, SWEEP_END[0], SWEEP_END[2]),
      ...at(.33, SWEEP_CUT).slice(1, 2), S(.385, [.80, -.10, .59], [.70, -.06, .71]), ...at(.33, SWEEP_CUT).slice(2), S(.62, [-.55, -.14, .82], [-.88, -.10, .46]), S(.78, [.10, -.75, .65], [-.30, -.40, .86]), IDLE_S(.92)],
  },
  crusher: {
    label: 'Crusher claw', key: '압착 집게', duration: 1.1, blend: .07, active: [.62, .78], impact: .70, slam: true, fx: [[.24, 'grab'], [.70, 'crush']],
    steps: [['R', .10, .24]], root: [[0, 0], [.10, 0], [.24, .4], [1.1, .4]],
    keys: [key(0), key(.22, reach, [0, -.14, .14], 'out'), key(.34, reach, [0, -.14, .14]),
      key(.52, { pelvis: [-2, 10, 0], spine: [-6, 4, 0], chest: [-10, 10, 0], neck: [4, -6, 0], head: [2, -6, 0], ...lungeR, ...L_out }, [0, .02, .06]),
      key(.70, crushHold, [0, -.26, .12], 'out'), key(.90, crushHold, [0, -.25, .12]), key(1.1)],
    weapon: [IDLE_S(0), S(.10, [.30, -.40, .87], [.10, .10, .99]), S(.22, [.18, -.15, .97], [.10, -.10, .99]), S(.34, [.18, -.15, .97], [.10, -.10, .99]),
      S(.44, [.30, .35, .89], [.15, .55, .82]), S(.54, [.30, .60, .74], [.10, .85, .52]), S(.64, [.25, .20, .95], [.05, .40, .91]), S(.70, [.20, -.25, .95], [0, -.72, .69]),
      S(.90, [.20, -.27, .94], [0, -.72, .69]), S(1.0, [.30, -.70, .65], [-.05, -.10, .99]), IDLE_S(1.1)],
  },
  sawblade: {
    label: 'Rev saw', key: '회전 톱날', duration: .65, blend: .07, fx: [[.22, 'sawRev']],
    keys: [key(0), key(.20, sawPose, [0, -.10, 0]), key(.45, sawPose, [0, -.10, 0]), key(.65)],
    weapon: [IDLE_S(0), S(.12, [.20, -.50, .84], [-.20, .70, .68]), S(.20, [.20, -.40, .89], [-.30, .95, .10]), S(.45, [.20, -.40, .89], [-.30, .95, .10]), S(.55, [.25, -.70, .67], [-.15, .55, .82]), IDLE_S(.65)],
  },
  thornwhip: {
    label: 'Thorn whip', key: '가시 채찍', duration: 1.0, blend: .07, active: [.20, .36], impact: .27, fx: [[.27, 'whip'], [.54, 'whipPull']],
    yaw: [[0, 0], [.12, 20], [.32, -50], [.50, -50], [.66, -20], [1.0, 0]],
    steps: [['R', .12, .26]], root: [[0, 0], [.12, 0], [.28, .5], [1.0, .5]],
    keys: [key(0),
      key(.12, { pelvis: [10, 26, 0], spine: [6, 10, 0], chest: [4, 30, 0], neck: [0, -14, 0], head: [-4, -14, 0], Rthigh: [-34, 0, -3], Rshin: [52, 0, 0], ...L_front }, [0, -.18, .04], 'in'),
      key(.28, { pelvis: [12, -18, 0], spine: [8, -6, 0], chest: [6, -22, 0], neck: [-2, 10, 0], head: [-6, 10, 0], ...lungeR, ...L_back }, [-.02, -.20, .14], 'out'),
      key(.42, l2Hold, [-.03, -.20, .15]), key(.56, pullBack, [0, -.06, -.06], 'out'), key(.72, pullBack, [0, -.07, -.06]), key(1.0)],
    weapon: [IDLE_S(0), S(.06, ...MID), S(.10, [.60, -.60, -.10], [.60, -.55, .10]), ...at(.18, SWEEP_CUT), S(.44, [-.55, -.14, .82], [-.88, -.10, .46]),
      S(.52, [-.20, -.40, .89], [-.60, -.30, .74]), S(.58, [.30, -.60, .74], [.10, -.40, .91]), S(.72, [.45, -.80, .40], [.30, -.50, .81]), S(.86, [.30, -.85, .40], [-.05, .10, .99]), IDLE_S(1.0)],
  },
  // ---- 팔 (왼팔 도구) ----
  net: {
    label: 'Net launcher', key: '그물 발사기', duration: .75, blend: .06, fx: [[.26, 'net']],
    steps: [['L', .06, .20]], root: [[0, 0], [.06, 0], [.20, .15], [.75, .15]],
    keys: [key(0), key(.18, { ...LL, ...L_aim }, [0, -.12, .04], 'out'), key(.25, { ...LL, ...L_aim }, [0, -.12, .04]),
      key(.30, { ...LL, pelvis: [2, 26, 0], LupperArm: [-110, 0, -14], Lforearm: [-30, 0, 0] }, [0, -.10, 0], 'out'),
      key(.50, { ...LL, LupperArm: [-80, 0, -14], Lforearm: [-25, 0, 0] }, [0, -.10, .02]), key(.75)],
    weapon: park(.75),
  },
  bulwark: {
    label: 'Shield', key: '방패 전개', duration: .6, blend: .06, fx: [[.20, 'shield']],
    keys: [key(0), key(.20, guardPose, [0, -.16, 0], 'out'), key(.42, guardPose, [0, -.15, 0]), key(.6)],
    weapon: park(.6),
  },
  shockpalm: {
    label: 'Shock palm', key: '전류 손바닥', duration: .8, blend: .06, active: [.26, .40], impact: .28, fx: [[.28, 'shock']],
    steps: [['L', .08, .22]], root: [[0, 0], [.08, 0], [.24, .5], [.8, .5]],
    keys: [key(0), key(.12, { ...LL, pelvis: [8, 10, 0], chest: [2, 0, 0], neck: [0, -6, 0], head: [-4, -6, 0], LupperArm: [-30, 0, -14], Lforearm: [-100, 0, 0] }, [0, -.12, -.02], 'in'),
      key(.26, palmHold, [0, -.16, .10], 'out'), key(.50, palmHold, [0, -.15, .10]), key(.8)],
    weapon: park(.8),
  },
  mine: {
    label: 'Sticky mine', key: '점착 지뢰', duration: .75, blend: .06, fx: [[.32, 'mine']],
    steps: [['L', .08, .24]], root: [[0, 0], [.08, 0], [.24, .25], [.75, .25]],
    keys: [key(0), key(.30, mineHold, [0, -.30, .06], 'out'), key(.42, mineHold, [0, -.30, .06]), key(.75)],
    weapon: park(.75),
  },
  acid: {
    label: 'Acid spray', key: '산성 분사', duration: 1.0, blend: .06, active: [.22, .70], impact: .22, multi: .15, fx: [[.22, 'acid']],
    yaw: [[0, 0], [.20, 16], [.70, -16], [1.0, 0]],
    steps: [['L', .06, .18]], root: [[0, 0], [.06, 0], [.18, .12], [1.0, .12]],
    keys: [key(0), key(.18, { ...LL, ...L_aim }, [0, -.12, .04], 'out'), key(.72, { ...LL, ...L_aim }, [0, -.12, .04]), key(1.0)],
    weapon: park(1.0),
  },
  suck: {
    label: 'Intake', key: '흡입', duration: .95, blend: .06, fx: [[.10, 'suck']],
    keys: [key(0), key(.12, braced, [0, -.12, -.08], 'out'), key(.60, braced, [0, -.13, -.09]),
      key(.72, { ...LL, LupperArm: [-60, 0, -14], Lforearm: [-70, 0, 0] }, [0, -.10, -.04], 'out'), key(.95)],
    weapon: park(.95),
  },
  // ---- 다리 ----
  spiderfoot: {
    label: 'Anchor legs', key: '거미 발 고정', duration: .75, blend: .06, air: [.10, .34], land: .36, arc: [.10, .36, .35], fx: [[.38, 'anchor']],
    root: [[0, 0], [.75, 0]],
    keys: [key(0), key(.08, { ...crouchJ, ...L_out }, [0, -.20, 0], 'in'),
      key(.22, { pelvis: [0, -10, 0], Rthigh: [-30, 0, 18], Lthigh: [-10, 0, -18], Rshin: [40, 0, 0], Lshin: [40, 0, 0], ...L_out }, [0, .02, 0]),
      key(.40, anchorHold, [0, -.30, 0], 'out'), key(.58, anchorHold, [0, -.28, 0]), key(.75)],
    weapon: [IDLE_S(0), S(.08, ...DOWNF), S(.30, [.60, -.75, .20], [.50, -.75, .40]), S(.40, OUT[0], OUT[2]), S(.58, OUT[0], OUT[2]), S(.68, ...LOWOUT), IDLE_S(.75)],
  },
  tremor: {
    label: 'Tremor step', key: '진동 걸음', duration: .7, blend: .06, impact: .36, fx: [[.36, 'stomp']],
    steps: [['R', .08, .36]], root: [[0, 0], [.08, 0], [.36, .25], [.7, .25]],
    keys: [key(0),
      key(.22, { pelvis: [-2, -12, 0], spine: [-4, 0, 0], chest: [-4, 6, 0], neck: [2, 0, 0], head: [0, 0, 0], Rthigh: [-85, 0, -4], Rshin: [95, 0, 0], Rfoot: [10, 0, 0], Lthigh: [0, 0, 5], Lshin: [20, 0, 0], ...L_out }, [0, .04, 0], 'out'),
      key(.36, { ...landHold, pelvis: [22, -10, 0] }, [0, -.24, .04], 'in'), key(.52, landHold, [0, -.22, .04]), key(.7)],
    weapon: [IDLE_S(0), S(.20, [.40, -.80, .45], [.30, -.20, .93]), S(.36, [.70, -.65, .30], [.65, -.55, .52]), S(.55, [.70, -.65, .30], [.65, -.55, .52]), IDLE_S(.7)],
  },
  piston: {
    label: 'Piston charge', key: '피스톤 돌진', duration: .85, blend: .05, active: [.08, .44], impact: .10, multi: .12, air: [.06, .40], land: .40, fx: [[.07, 'charge']],
    root: [[0, 0], [.08, .1], [.40, 5.0], [.48, 5.2], [.85, 5.2]],
    keys: [key(0), key(.07, chargeHold, [0, -.20, .06], 'out'), key(.40, chargeHold, [0, -.20, .06]),
      key(.50, { pelvis: [14, 0, 0], chest: [6, 0, 0], ...lungeR, ...L_out }, [0, -.16, .03], 'out'), key(.85)],
    weapon: park(.85, .06),
  },
  pounce: {
    label: 'Pounce', key: '도약 갈퀴', duration: 1.2, blend: .06, active: [.54, .64], impact: .58, slam: true, air: [.12, .56], land: .56, arc: [.14, .56, 1.4], fx: [[.58, 'pounce']],
    steps: [['L', .68, .82], ['R', .76, .92]],
    root: [[0, 0], [.14, 0], [.56, 5], [.62, 5.05], [.90, 3.9], [1.2, 3.85]],
    keys: [key(0), key(.12, { ...crouchJ, ...L_back }, [0, -.24, 0], 'in'),
      key(.30, { pelvis: [-4, -10, 0], ...tucked, ...L_out }, [0, 0, 0]),
      key(.48, { pelvis: [10, -10, 0], Rthigh: [-20, 0, -7], Rshin: [30, 0, 0], Lthigh: [-40, 0, 7], Lshin: [60, 0, 0], ...L_out }, [0, 0, 0]),
      key(.58, { ...landHold, pelvis: [22, -10, 0] }, [0, -.28, .04], 'out'),
      key(.80, { pelvis: [-6, -10, 0], spine: [-6, 0, 0], chest: [-6, 4, 0], Rthigh: [0, 0, -5], Lthigh: [14, 0, 5], Rshin: [40, 0, 0], Lshin: [50, 0, 0], ...L_front }, [0, -.10, -.04]),
      key(1.0, { pelvis: [14, -10, 0], ...lungeR, ...L_back }, [0, -.14, 0]), key(1.2)],
    weapon: [IDLE_S(0), S(.12, [.30, -.90, .30], [0, -.10, 1]), S(.40, [.50, -.65, .57], [.10, .25, .96]), S(.50, [.40, -.30, .87], [.10, .10, .99]),
      S(.58, [.25, -.60, .76], [0, -.72, .69]), S(.72, [.30, -.85, .40], [0, -.40, .92]), S(1.0, [.30, -.85, .40], [-.10, .10, .99]), IDLE_S(1.2)],
  },
  slam: {
    label: 'Impact landing', key: '충격 착지', duration: .9, blend: .06, active: [.40, .48], impact: .42, slam: true, air: [.08, .42], land: .42, arc: [.10, .42, .9], fx: [[.42, 'slamWave']],
    root: [[0, 0], [.10, 0], [.42, .6], [.9, .6]],
    keys: [key(0), key(.08, { ...crouchJ, ...L_back }, [0, -.24, 0], 'in'),
      key(.22, { pelvis: [-4, 0, 0], Lthigh: [-50, 0, 7], Rthigh: [-25, 0, -7], Lshin: [80, 0, 0], Rshin: [90, 0, 0], ...L_out }, [0, 0, 0]),
      key(.36, { pelvis: [14, 0, 0], chest: [10, 0, 0], Lthigh: [-40, 0, 7], Rthigh: [-20, 0, -7], Lshin: [60, 0, 0], Rshin: [70, 0, 0], ...L_back }, [0, 0, .05], 'in'),
      key(.44, landHold, [0, -.30, .10], 'out'), key(.62, landHold, [0, -.28, .10]), key(.9)],
    weapon: [IDLE_S(0), S(.10, [.50, -.65, .57], [.10, .25, .96]), S(.22, [.75, .20, .60], [.30, .85, .40]), S(.34, [.30, .20, .93], [0, .75, .66]),
      S(.39, [.25, 0, .97], [0, .10, 1]), S(.43, [.20, -.25, .95], [0, -.72, .69]), S(.62, [.20, -.27, .94], [0, -.72, .69]), S(.78, [.30, -.70, .65], [-.05, -.10, .99]), IDLE_S(.9)],
  },
  smoke: {
    label: 'Smoke burst', key: '연막 분사', duration: .6, blend: .05, air: [.18, .42], land: .42, fx: [[.12, 'smoke']],
    root: [[0, 0], [.16, 0], [.36, -1.3], [.46, -1.4], [.6, -1.4]],
    keys: [key(0), key(.12, { pelvis: [30, -10, 0], spine: [14, 0, 0], chest: [14, 6, 0], neck: [-10, 0, 0], head: [-14, 0, 0], Lthigh: [-50, 0, 7], Rthigh: [-60, 0, -7], Lshin: [90, 0, 0], Rshin: [95, 0, 0], ...L_tuck }, [0, -.32, 0], 'out'),
      key(.30, { pelvis: [-4, -12, 0], Rthigh: [8, 0, -5], Lthigh: [22, 0, 5], Rshin: [40, 0, 0], Lshin: [55, 0, 0], ...L_out }, [0, .10, -.02]),
      key(.46, { pelvis: [16, -12, 0], Rthigh: [-36, 0, -5], Lthigh: [-12, 0, 5], Rshin: [58, 0, 0], Lshin: [50, 0, 0], ...L_out }, [0, -.15, 0]), key(.6)],
    weapon: [IDLE_S(0), S(.12, [.30, -.90, .30], [-.10, .10, .99]), S(.30, [.40, -.80, .45], [.05, .35, .94]), S(.46, [.30, -.90, .30], [-.10, .10, .99]), IDLE_S(.6)],
  },
  // ---- 몸통 (코어) ----
  shield: {
    label: 'Barrier', key: '보호막 발생기', duration: .75, blend: .06, fx: [[.30, 'shieldUp']],
    keys: [key(0), key(.12, { pelvis: [16, -10, 0], spine: [12, 0, 0], chest: [14, 4, 0], neck: [-4, 0, 0], head: [-8, 0, 0], ...L_tuck, Rthigh: [-30, 0, -3], Rshin: [48, 0, 0] }, [0, -.18, 0], 'in'),
      key(.30, openChest, [0, 0, -.02], 'out'), key(.50, openChest, [0, 0, -.02]), key(.75)],
    weapon: [IDLE_S(0), S(.12, ...DOWNF), S(.30, [.90, -.30, .30], [.90, -.25, .36]), S(.50, [.90, -.30, .30], [.90, -.25, .36]), S(.64, ...LOWOUT), IDLE_S(.75)],
  },
  discharge: {
    label: 'Discharge', key: '방전 코어', duration: .85, blend: .06, impact: .38, fx: [[.38, 'discharge']],
    keys: [key(0), key(.20, curl, [0, -.26, 0], 'in'), key(.38, burst, [0, .04, 0], 'out'), key(.58, burst, [0, .02, 0]), key(.85)],
    weapon: [IDLE_S(0), S(.10, ...DOWNF), S(.20, [.20, -.80, .55], [-.10, -.50, .86]), S(.38, [.90, -.05, .40], [.90, .15, .40]), S(.58, [.90, -.05, .40], [.90, .15, .40]), S(.72, ...LOWOUT), IDLE_S(.85)],
  },
  vent: {
    label: 'Heat vent', key: '과열 배출', duration: .8, blend: .06, impact: .30, fx: [[.28, 'vent']],
    keys: [key(0), key(.18, hunch, [0, -.26, -.04], 'out'), key(.30, { ...hunch, spine: [24, 0, 4], chest: [26, 4, 6] }, [0, -.27, -.05]),
      key(.42, { ...hunch, spine: [20, 0, -4], chest: [22, 4, -6] }, [0, -.26, -.04]), key(.60, hunch, [0, -.25, -.04]), key(.8)],
    weapon: [IDLE_S(0), S(.10, ...DOWNF), S(.18, [.60, -.75, .20], [.50, -.75, .40]), S(.60, [.60, -.75, .20], [.50, -.75, .40]), S(.72, ...LOWOUT), IDLE_S(.8)],
  },
  spore: {
    label: 'Spore release', key: '포자 방출', duration: .85, blend: .06, fx: [[.35, 'spore']],
    keys: [key(0), key(.15, shake(1), [0, -.14, 0]), key(.27, shake(-1), [0, -.14, 0]), key(.39, shake(1), [0, -.14, 0]), key(.51, shake(-1), [0, -.14, 0]),
      key(.65, { pelvis: [10, -10, 0], chest: [8, 4, 0] }, [0, -.10, 0]), key(.85)],
    weapon: [IDLE_S(0), S(.12, ...LOWOUT), S(.65, ...LOWOUT), IDLE_S(.85)],
  },
  resonance: {
    label: 'Resonance pulse', key: '공명 파동', duration: 1.05, blend: .07, impact: .55, fx: [[.55, 'resonance']],
    keys: [key(0), key(.25, gather, [0, -.24, 0], 'in'), key(.55, skyward, [0, .02, 0], 'out'), key(.80, skyward, [0, .01, 0]), key(1.05)],
    weapon: [IDLE_S(0), S(.15, ...DOWNF), S(.25, [.20, -.80, .55], [-.10, -.50, .86]), S(.55, [.85, -.35, .40], [.85, -.20, .48]), S(.80, [.85, -.35, .40], [.85, -.20, .48]), S(.92, ...LOWOUT), IDLE_S(1.05)],
  },
  // ---- 머리 ----
  flash: {
    label: 'Flash lens', key: '섬광 렌즈', duration: .55, blend: .05, fx: [[.20, 'flash']],
    keys: [key(0), key(.14, { pelvis: [10, -10, 0], spine: [4, 0, 0], chest: [-4, 6, 0], neck: [-14, 0, 0], head: [-14, 0, 0], ...L_back }, [0, -.10, -.04], 'in'),
      key(.20, headThrust, [0, -.12, .06], 'out'), key(.34, headThrust, [0, -.12, .06]), key(.55)],
    weapon: [IDLE_S(0), S(.10, ...DOWNF), S(.40, ...DOWNF), IDLE_S(.55)],
  },
  screech: {
    label: 'Sonic screech', key: '음파 비명', duration: .9, blend: .06, impact: .34, fx: [[.34, 'screech']],
    keys: [key(0), key(.20, { pelvis: [0, -10, 0], spine: [-12, 0, 0], chest: [-18, 6, 0], neck: [-25, 0, 0], head: [-25, 0, 0], LupperArm: [30, 0, -40], Lforearm: [-20, 0, 0] }, [0, -.06, -.04], 'in'),
      key(.34, screamOut, [0, -.18, .08], 'out'), key(.60, screamOut, [0, -.17, .08]), key(.9)],
    weapon: [IDLE_S(0), S(.10, ...MID), S(.20, ...BACK), S(.60, ...BACK), S(.75, ...MID), IDLE_S(.9)],
  },
  mark: {
    label: 'Target mark', key: '표적 지정', duration: .55, blend: .05, fx: [[.20, 'mark']],
    keys: [key(0), key(.18, pointPose, [0, -.10, .02], 'out'), key(.38, pointPose, [0, -.10, .02]), key(.55)],
    weapon: park(.55),
  },
  drone: {
    label: 'Recon drone', key: '드론 정찰', duration: .75, blend: .06, fx: [[.38, 'drone']],
    keys: [key(0), key(.20, { pelvis: [6, -24, 0], spine: [0, -8, 0], chest: [-4, -18, 0], neck: [0, 14, 0], head: [-4, 14, 0], LupperArm: [-150, 0, -20], Lforearm: [-120, 0, 0] }, [0, -.08, 0], 'in'),
      key(.38, tossHold, [0, -.10, .04], 'out'), key(.55, tossHold, [0, -.10, .04]), key(.75)],
    weapon: park(.75),
  },
});
/// the slot-part actives above, in catalogue order (audit / preview)
export const SLOT_MOVES = ['harpoon', 'heatcut', 'crusher', 'sawblade', 'thornwhip', 'net', 'bulwark', 'shockpalm', 'mine', 'acid', 'suck', 'spiderfoot', 'tremor', 'piston', 'pounce', 'slam', 'smoke', 'shield', 'discharge', 'vent', 'spore', 'resonance', 'flash', 'screech', 'mark', 'drone'];
for (const name of SLOT_MOVES) moves[name].root ??= [[0, 0], [moves[name].duration, 0]];   // in-place moves

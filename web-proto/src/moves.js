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

// after the descending cut: weight on the right (front) foot, torso turned left and forward
const l1Hold = { pelvis: [12, -32, 0], spine: [8, -10, 0], chest: [8, -30, 0], neck: [-4, 16, 0], head: [-8, 16, 0], ...lungeR, ...L_back };
// after the rising cut: left foot forward, torso turned right and extended upward
const l2Hold = { pelvis: [8, 36, 0], spine: [6, 12, 0], chest: [4, 42, 0], neck: [-2, -18, 0], head: [-6, -18, 0], ...lungeL, ...L_out };
const spinLean = { pelvis: [10, 0, -10], spine: [6, 0, -8], chest: [6, 0, -10], neck: [0, 0, 8], head: [-4, 0, 6], Rthigh: [-30, 0, -10], Rshin: [44, 0, 0], Lthigh: [-8, 0, 12], Lshin: [44, 0, 0], ...L_out };
const l3Hold = { pelvis: [14, -28, 0], spine: [8, -10, 0], chest: [8, -30, 0], neck: [-4, 16, 0], head: [-8, 16, 0], ...lungeR, ...L_back };
const charge = { pelvis: [8, 48, 0], spine: [4, 10, 0], chest: [4, 22, 0], neck: [0, -28, 0], head: [-6, -30, 0], Rthigh: [-30, 0, -3], Rshin: [44, 0, 0], Lthigh: [20, 0, 5], Lshin: [36, 0, 0], ...L_front };
const spinPose = { pelvis: [4, 0, -6], spine: [0, 0, -8], chest: [0, 0, -10], neck: [0, 0, 6], head: [0, 0, 4], Rthigh: [-20, 0, -8], Rshin: [30, 0, 0], Lthigh: [-10, 0, 10], Lshin: [34, 0, 0], ...L_out };
const airSpin = { pelvis: [10, 0, -10], spine: [6, 0, -8], chest: [4, 0, -8], neck: [0, 0, 6], ...tucked, ...L_out };
const lungeHold = { ...deepR, pelvis: [23, -10, 0], spine: [12, 0, 0], chest: [8, 8, 0], neck: [-8, -4, 0], head: [-12, -4, 0], ...L_back };
const landHold = { pelvis: [18, -10, 0], spine: [12, 0, 0], chest: [10, 0, 0], neck: [-6, 0, 0], head: [-12, 0, 0], ...lungeR, ...L_back };
const finisher = { pelvis: [10, -22, 0], spine: [6, -6, 0], chest: [6, -26, 0], neck: [0, 14, 0], head: [-4, 14, 0], ...lungeR, ...L_back };

export const moves = {
  light1: {
    label: 'Falling cut', key: 'J', duration: .78, blend: .08, active: [.20, .33], impact: .27, buffer: [.30, .74], chain: 'light2', chainAt: .48,
    steps: [['R', .10, .24], ['L', .46, .68]],
    root: [[0, 0], [.12, 0], [.25, .38], [.34, .42], [.78, .42]],
    keys: [
      key(0),
      key(.08, { pelvis: [0, 14, 0], spine: [-4, 6, 0], chest: [-6, 14, 0], neck: [0, -8, 0], head: [0, -8, 0], ...L_front }, [0, .03, -.03]),
      key(.16, { pelvis: [-2, 30, 0], spine: [-6, 10, 0], chest: [-8, 28, 0], neck: [2, -16, 0], head: [0, -16, 0], Rthigh: [-30, 0, -3], Rshin: [42, 0, 0], ...L_front }, [.02, .05, -.05], 'in'),
      key(.21, { pelvis: [6, 6, 0], spine: [2, 2, 0], chest: [0, 8, 0], neck: [0, -4, 0], head: [-2, -4, 0], Rthigh: [-46, 0, -4], Rshin: [50, 0, 0], Lthigh: [20, 0, 5], Lshin: [26, 0, 0], ...L_rest }, [0, -.04, .05]),
      key(.25, { pelvis: [12, -24, 0], spine: [8, -8, 0], chest: [8, -22, 0], neck: [-4, 12, 0], head: [-8, 12, 0], ...lungeR, ...L_back }, [-.02, -.13, .10], 'out'),
      key(.31, l1Hold, [-.03, -.15, .11]),
      key(.48, l1Hold, [-.03, -.14, .10]),
      key(.64, { pelvis: [6, -6, 0], chest: [4, -4, 0], Lthigh: [0, 0, 5], Lshin: [38, 0, 0] }, [0, -.05, .03]),
      key(.78),
    ],
  },
  light2: {
    label: 'Backhand sweep', key: 'A²', duration: .80, blend: .07, active: [.15, .35], impact: .24, buffer: [.28, .76], chain: 'light3', chainAt: .50,
    steps: [['L', .08, .22], ['R', .50, .70]],
    root: [[0, 0], [.08, 0], [.22, .30], [.30, .34], [.80, .34]],
    keys: [
      key(0, l1Hold, [-.03, -.14, .10]),
      key(.10, { pelvis: [14, -46, 0], spine: [10, -14, 0], chest: [8, -42, 0], neck: [-4, 20, 0], head: [-6, 20, 0], ...lungeR, Lthigh: [-14, 0, 5], Lshin: [46, 0, 0], ...L_front }, [-.02, -.17, .05], 'in'),
      key(.16, { pelvis: [14, -48, 0], spine: [10, -14, 0], chest: [8, -44, 0], neck: [-4, 22, 0], head: [-6, 22, 0], ...lungeR, Lthigh: [-24, 0, 5], Lshin: [44, 0, 0], ...L_front }, [-.02, -.18, .06]),
      key(.22, { pelvis: [10, 18, 0], spine: [6, 8, 0], chest: [4, 26, 0], neck: [-2, -12, 0], head: [-6, -12, 0], ...lungeL, ...L_out }, [.02, -.14, .10], 'out'),
      key(.30, l2Hold, [.03, -.15, .11]),
      key(.48, l2Hold, [.03, -.14, .10]),
      key(.64, { pelvis: [6, 4, 0], chest: [4, 8, 0], Rthigh: [-8, 0, -3], Rshin: [40, 0, 0] }, [0, -.05, .03]),
      key(.80),
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
const L1_END = [[.10, -.58, .81], [-.35, -.62, .70], [-.70, -.57, .43]];
const SWEEP_END = [[.85, -.30, -.40], [.70, -.30, -.65], [.70, -.30, -.65]];
const CHARGE = [[.38, -.66, -.65], [.35, -.63, -.69], [.40, -.33, -.86]];
const OUT = [[.90, -.40, .15], [.90, -.42, .12], [.88, -.45, .10]];
const ARM_GUIDES = {
  // diagonal descending cut (upper right -> lower left)
  light1: [idleA(0),
    A(.05, [.60, -.40, .20], [.30, .10, .95], [.10, .85, .50]),
    A(.09, [.60, .10, .05], [.15, .75, .60], [-.05, .95, -.30]),
    A(.16, [.58, .50, -.15], [0, .98, .15], [-.25, .30, -.92]),       // cocked over the right shoulder
    A(.20, [.55, .25, .55], [.10, .40, .91], [.05, .90, -.35]),       // hands lead, blade still lags
    A(.225, [.48, .05, .87], [.05, .05, 1], [-.15, .55, .82]),        // wrist releases
    A(.25, [.35, -.20, .92], [0, -.35, .94], [-.45, -.45, .77]),      // contact
    A(.31, L1_END[0], L1_END[1], L1_END[2]),                          // follow-through to the left hip
    A(.48, [.10, -.60, .80], [-.35, -.64, .68], [-.70, -.58, .41]),
    A(.64, [.30, -.85, .40], [0, -.55, .83], [-.25, -.10, .96]),
    idleA(.78)],
  // rising diagonal (lower left -> upper right), flows out of the first cut
  // backhand horizontal sweep at waist-chest height, outward from the body (left -> right)
  light2: [A(0, L1_END[0], L1_END[1], L1_END[2]),
    A(.10, [-.30, -.40, .87], [-.80, -.15, -.58], [-.80, -.15, -.58]),
    A(.16, [-.35, -.35, .87], [-.78, -.10, -.62], [-.78, -.10, -.62]),     // coiled far back across the body
    A(.19, [-.30, -.25, .92], [-.75, -.15, .64], [-.75, -.15, .64]),
    A(.22, [.20, -.25, .95], [0, -.18, .98], [0, -.18, .98]),
    A(.25, [.75, -.25, .62], [.80, -.20, .56], [.80, -.20, .56]),         // contact, arm straight
    A(.29, [.92, -.28, .10], [.95, -.22, -.15], [.95, -.22, -.15]),
    A(.34, SWEEP_END[0], SWEEP_END[1], SWEEP_END[2]),                     // follow-through well behind the right side
    A(.48, [.85, -.32, -.40], [.70, -.32, -.64], [.70, -.32, -.64]),
    A(.56, [.70, -.55, .05], [.80, -.45, .10], [.80, -.45, .10]),
    A(.64, [.45, -.80, .40], [.40, -.45, .80], [.40, -.45, .80]),
    idleA(.80)],
  // full-circle sweep: arm extended outward-down while the whole body spins once, closing cut across the front
  light3: [A(0, SWEEP_END[0], SWEEP_END[1], SWEEP_END[2]),
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

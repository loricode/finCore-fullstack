import { ChangeDetectionStrategy, Component } from '@angular/core';

/**
 * Ilustración del login: una sala acogedora (sofá verde azulado, manta rosa, lámpara, plantas, ventana con cortinas
 * ocre y un gato dormido). SVG en línea: no hace peticiones extra, pesa pocos KB y usa los tokens de color del tema.
 */
@Component({
  selector: 'fc-login-illustration',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <svg viewBox="0 0 440 360" role="img" aria-labelledby="ill-title" xmlns="http://www.w3.org/2000/svg">
      <title id="ill-title">Una persona trabajando tranquila en su sofá, con un gato dormido a sus pies</title>
      <defs>
        <linearGradient id="sky" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stop-color="#cfe6e2" /><stop offset="1" stop-color="#f6efe4" />
        </linearGradient>
        <linearGradient id="sofa" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stop-color="#2a7375" /><stop offset="1" stop-color="#1b5557" />
        </linearGradient>
      </defs>

      <!-- pared y luz -->
      <ellipse cx="220" cy="190" rx="205" ry="160" fill="var(--fc-linen)" />
      <!-- ventana -->
      <path d="M58 70 a52 52 0 0 1 104 0 v110 h-104z" fill="url(#sky)" stroke="#e8dfd2" stroke-width="5" />
      <path d="M110 18 v162 M58 120 h104" stroke="#e8dfd2" stroke-width="4" />
      <circle cx="132" cy="62" r="11" fill="#f6e8cf" />
      <!-- cortinas -->
      <path d="M44 22 h18 c-6 50 4 110 -2 168 h-20 c4 -58 -6 -118 4 -168z" fill="#c9a46a" opacity=".9" />
      <path d="M158 22 h18 c6 50 -4 110 2 168 h-20 c-4 -58 6 -118 0 -168z" fill="#c9a46a" opacity=".9" />
      <rect x="36" y="16" width="148" height="6" rx="3" fill="#8a7152" />

      <!-- cuadro -->
      <rect x="262" y="40" width="70" height="86" rx="4" fill="#fbf8f2" stroke="#d8b98f" stroke-width="5" />
      <path d="M297 112 v-46 M297 82 c-12 -4 -16 -14 -14 -22 M297 74 c10 -4 14 -12 12 -20 M297 96 c-10 -2 -15 -9 -15 -16" stroke="#8fa58a" stroke-width="3" fill="none" stroke-linecap="round" />

      <!-- lámpara -->
      <path d="M384 120 h-36 l10 -38 h16z" fill="#f2e3c8" />
      <rect x="364" y="120" width="4" height="150" fill="#8a7152" />
      <ellipse cx="366" cy="272" rx="18" ry="5" fill="#8a7152" />
      <ellipse cx="366" cy="128" rx="34" ry="10" fill="#f6e8cf" opacity=".55" class="glow" />

      <!-- piso y alfombra -->
      <rect x="20" y="300" width="400" height="8" rx="4" fill="#d8b98f" opacity=".7" />
      <path d="M90 318 h260 l-20 26 h-220z" fill="#cfe6e2" />
      <path d="M220 320 l26 11 l-26 11 l-26 -11z M150 320 l20 11 l-20 11 l-20 -11z M290 320 l20 11 l-20 11 l-20 -11z" fill="#f8e0d8" />

      <!-- sofá -->
      <rect x="96" y="196" width="232" height="64" rx="22" fill="url(#sofa)" />
      <rect x="110" y="232" width="204" height="44" rx="14" fill="#236769" />
      <rect x="82" y="218" width="40" height="66" rx="16" fill="#1f6264" />
      <rect x="302" y="218" width="40" height="66" rx="16" fill="#1f6264" />
      <rect x="104" y="284" width="8" height="16" rx="2" fill="#8a7152" />
      <rect x="312" y="284" width="8" height="16" rx="2" fill="#8a7152" />
      <!-- cojines y manta -->
      <rect x="120" y="206" width="46" height="36" rx="12" fill="#e6b3a3" transform="rotate(-8 143 224)" />
      <rect x="262" y="206" width="44" height="36" rx="12" fill="#c3b3e3" transform="rotate(7 284 224)" />
      <path d="M276 238 c18 -2 30 4 34 14 l-6 40 c-12 4 -22 2 -30 -4z" fill="#f2c4b5" />
      <path d="M282 252 l22 2 M280 264 l24 2 M279 276 l22 2" stroke="#e6a493" stroke-width="2" />

      <!-- persona -->
      <path d="M182 240 c0 -30 14 -46 34 -46 s34 16 34 46z" fill="#c9a46a" />
      <path d="M200 240 c-8 0 -40 2 -52 18 c-4 8 2 14 10 14 h40z" fill="#3a5f7a" />
      <path d="M240 240 c8 0 36 2 46 18 c4 8 -2 14 -10 14 h-36z" fill="#3a5f7a" />
      <path d="M152 270 h16 v26 h-20 a6 6 0 0 1 4 -10z M262 270 h16 v16 a6 6 0 0 1 4 10 h-20z" fill="#3a3632" />
      <circle cx="216" cy="172" r="22" fill="#d9a07f" />
      <path d="M194 168 c0 -18 12 -26 24 -26 c14 0 22 10 22 22 c-6 -6 -16 -8 -26 -6 c-8 2 -14 6 -20 10z" fill="#3a3632" />
      <circle cx="208" cy="174" r="2" fill="#3a3632" /><circle cx="224" cy="174" r="2" fill="#3a3632" />
      <path d="M210 184 q6 5 12 0" stroke="#3a3632" stroke-width="2" fill="none" stroke-linecap="round" />
      <!-- portátil -->
      <path d="M184 228 h64 l-8 -34 h-48z" fill="#e8dfd2" />
      <rect x="176" y="228" width="80" height="6" rx="3" fill="#cfc4b4" />
      <circle cx="216" cy="211" r="5" fill="#8fa58a" />
      <path d="M196 236 c6 -6 14 -6 20 -2 M236 236 c-6 -6 -14 -6 -20 -2" stroke="#d9a07f" stroke-width="7" stroke-linecap="round" fill="none" />

      <!-- mesa auxiliar con taza -->
      <rect x="350" y="262" width="52" height="6" rx="3" fill="#8a7152" />
      <rect x="372" y="268" width="6" height="32" fill="#8a7152" />
      <rect x="364" y="246" width="18" height="16" rx="3" fill="#fbf8f2" stroke="#c9a46a" stroke-width="2" />
      <path d="M382 250 q8 4 0 9" stroke="#c9a46a" stroke-width="2" fill="none" />
      <path class="steam" d="M370 240 q-4 -8 2 -14 q5 -6 0 -12" stroke="#b3a99c" stroke-width="2" fill="none" stroke-linecap="round" />
      <path class="steam steam--2" d="M377 240 q-4 -8 2 -14 q5 -6 0 -12" stroke="#b3a99c" stroke-width="2" fill="none" stroke-linecap="round" />

      <!-- plantas -->
      <path d="M42 300 l6 -28 h28 l6 28z" fill="#d9917e" />
      <path d="M60 272 c-4 -20 -18 -28 -26 -30 c4 12 12 22 24 30 M62 272 c2 -24 12 -36 22 -40 c-2 16 -10 30 -20 40 M60 272 c-12 -10 -26 -10 -32 -6 c10 8 20 10 32 8" fill="#8fa58a" />
      <!-- gato dormido -->
      <g class="cat">
        <ellipse cx="236" cy="326" rx="26" ry="11" fill="#b88a47" />
        <circle cx="258" cy="320" r="9" fill="#b88a47" />
        <path d="M252 313 l2 -7 l4 5 M262 312 l3 -6 l2 7" fill="#b88a47" />
        <path d="M254 321 q2 2 4 0 M260 321 q2 2 4 0" stroke="#3a3632" stroke-width="1.4" fill="none" />
        <path d="M212 330 q-14 2 -12 -8" stroke="#b88a47" stroke-width="5" fill="none" stroke-linecap="round" />
      </g>
    </svg>
  `,
  styles: `
    :host { display: block; }
    svg { width: 100%; height: auto; display: block; }
    .steam { animation: steam 3.2s ease-in-out infinite; opacity: 0; }
    .steam--2 { animation-delay: 1.6s; }
    @keyframes steam { 0% { opacity: 0; transform: translateY(4px); } 40% { opacity: .8; } 100% { opacity: 0; transform: translateY(-8px); } }
    .cat { animation: breathe 4s ease-in-out infinite; transform-origin: 236px 330px; }
    @keyframes breathe { 50% { transform: scale(1.03, 1.06); } }
    .glow { animation: glow 5s ease-in-out infinite; }
    @keyframes glow { 50% { opacity: .9; } }
  `,
})
export class LoginIllustrationComponent {}

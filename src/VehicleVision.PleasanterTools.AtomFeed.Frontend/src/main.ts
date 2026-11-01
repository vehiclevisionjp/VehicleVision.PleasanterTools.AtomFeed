// アイコン（Material Icons）と文字の書体（DesignReference の app-fonts.css。Zen Kaku Gothic New・Zen Maru Gothic・Myrica M）は
// 同梱する（CSP の font-src 'self' で配る）。書体は文字を絞った woff2 を unicode-range で区画に分け、使う区画だけ読み込む。
// 同梱の書体が読めない・範囲外の字は、app-base.css の候補（Hiragino Sans → Yu Gothic UI → … → system-ui）で表示する
import '@fontsource/material-icons';
import { mount } from 'svelte';
import App from './App.svelte';
// 色のトークン（明示型。既定はライト）→ アプリの基本 → 同梱の書体 → この画面の部品。この順で読み込む
import './styles/designreference/colors-explicit.css';
import './styles/designreference/app-base.css';
import './styles/designreference/app-fonts.css';
import './styles/app.scss';

mount(App, { target: document.getElementById('app')! });

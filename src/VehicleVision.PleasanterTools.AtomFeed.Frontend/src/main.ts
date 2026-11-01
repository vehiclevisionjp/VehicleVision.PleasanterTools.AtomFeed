// アイコン（Material Icons）は同梱する（CSP の font-src 'self' で配る）。
// 文字の書体は同梱しない。DesignReference の決まり（アプリは端末の書体を使う）に従い、app-base.css の
// 書体の候補（Noto Sans CJK JP → … → system-ui）で表示する
import '@fontsource/material-icons';
import { mount } from 'svelte';
import App from './App.svelte';
// 色のトークン（明示型。既定はライト）→ アプリの基本 → この画面の部品。この順で読み込む
import './styles/designreference/colors-explicit.css';
import './styles/designreference/app-base.css';
import './styles/app.scss';

mount(App, { target: document.getElementById('app')! });

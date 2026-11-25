<script setup>
import { onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { api, setUnauthorizedHandler } from './api'
import { auth, clearAuth, logout, refreshAuth } from './auth'
import OverviewView from './components/OverviewView.vue'
import ComponentsView from './components/ComponentsView.vue'
import InstancesView from './components/InstancesView.vue'
import AccountsView from './components/AccountsView.vue'
import DebugView from './components/DebugView.vue'
import LoginView from './components/LoginView.vue'
import SetupView from './components/SetupView.vue'
import ToastHost from './components/ToastHost.vue'

const tabs = [
  { key: 'overview', label: '概览' },
  { key: 'components', label: '组件' },
  { key: 'instances', label: '实例' },
  { key: 'debug', label: '调试' },
  { key: 'accounts', label: '账户' }
]

const tab = ref(readHash())
const overview = ref(null)
const online = ref(false)
const updatedAt = ref('')
const bootError = ref('')

/**
 * 从地址栏 hash 读页签，方便直接打开某个页面
 */
function readHash() {
  const key = location.hash.replace('#', '')
  return tabs.some(x => x.key === key) ? key : 'overview'
}

/**
 * 切换页签并同步 hash
 * @param {string} key 页签
 */
function select(key) {
  tab.value = key
  location.hash = key
}

function onHashChange() {
  tab.value = readHash()
}

/**
 * 拉取总览，顺便刷新连接状态
 */
async function refresh() {
  try {
    const res = await api.get('admin/overview')
    overview.value = res.data
    online.value = true
    updatedAt.value = new Date().toLocaleTimeString()
  } catch {
    online.value = false
  }
}

let timer = null

function startPolling() {
  stopPolling()
  refresh()
  timer = setInterval(refresh, 5000)
}

function stopPolling() {
  if (timer) {
    clearInterval(timer)
    timer = null
  }
}

/**
 * 读取登录状态
 */
async function boot() {
  try {
    await refreshAuth()
  } catch (e) {
    bootError.value = e.message
    auth.loaded = true
  }
}

async function doLogout() {
  await logout()
}

watch(
  () => auth.authenticated,
  value => {
    if (value) {
      startPolling()
    } else {
      stopPolling()
    }
  }
)

onMounted(async () => {
  setUnauthorizedHandler(() => clearAuth())
  window.addEventListener('hashchange', onHashChange)

  await boot()

  if (auth.authenticated) {
    startPolling()
  }
})

onBeforeUnmount(() => {
  stopPolling()
  window.removeEventListener('hashchange', onHashChange)
})
</script>

<template>
  <div v-if="!auth.loaded" class="boot">
    <span v-if="bootError" class="error">{{ bootError }}</span>
    <span v-else>加载中…</span>
  </div>

  <SetupView v-else-if="!auth.initialized" />

  <LoginView v-else-if="!auth.authenticated" />

  <div v-else class="app">
    <header class="topbar">
      <div class="brand">
        <h1>PlugBotSharp</h1>
        <span class="sub">管理面板</span>
      </div>

      <span class="spacer" />

      <span class="badge" :class="online ? 'ok' : 'bad'">
        <span class="dot" />
        {{ online ? '已连接' : '连接断开' }}
      </span>

      <span class="user-chip">{{ auth.user?.user }}</span>
      <button class="btn ghost" @click="doLogout">退出</button>
    </header>

    <nav class="nav">
      <button
        v-for="item in tabs"
        :key="item.key"
        :class="{ active: tab === item.key }"
        @click="select(item.key)"
      >
        {{ item.label }}
      </button>
    </nav>

    <OverviewView v-if="tab === 'overview'" :overview="overview" />
    <ComponentsView v-else-if="tab === 'components'" />
    <InstancesView v-else-if="tab === 'instances'" />
    <DebugView v-else-if="tab === 'debug'" />
    <AccountsView v-else />

    <div class="footer">
      每 5 秒自动刷新<template v-if="updatedAt"> · 上次更新 {{ updatedAt }}</template>
    </div>

    <ToastHost />
  </div>
</template>

<script setup>
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import StatCard from './components/StatCard.vue'

const status = ref(null)
const error = ref('')
const updatedAt = ref('')

let timer = null

async function refresh() {
  try {
    const res = await fetch('api/status', { cache: 'no-store' })
    if (!res.ok) {
      throw new Error('HTTP ' + res.status)
    }

    status.value = await res.json()
    error.value = ''
    updatedAt.value = new Date().toLocaleTimeString()
  } catch (e) {
    error.value = String(e)
  }
}

const online = computed(() => !error.value)
const version = computed(() => status.value?.version ?? '-')
const uptime = computed(() => status.value?.uptime ?? '-')
const plugins = computed(() => status.value?.plugins ?? '-')
const proxies = computed(() => status.value?.proxies ?? '-')

onMounted(() => {
  refresh()
  timer = setInterval(refresh, 5000)
})

onBeforeUnmount(() => {
  if (timer) {
    clearInterval(timer)
  }
})
</script>

<template>
  <div class="page">
    <header class="page-header">
      <h1>PlugBotSharp 管理面板</h1>
      <span class="spacer" />
      <span class="badge" :class="online ? 'ok' : 'bad'">
        <span class="dot" />
        {{ online ? '已连接' : '连接断开' }}
      </span>
    </header>

    <div class="grid">
      <StatCard label="运行时长" :value="uptime" />
      <StatCard label="插件数量" :value="plugins" />
      <StatCard label="代理数量" :value="proxies" />
      <StatCard label="核心版本" :value="version" />
    </div>

    <section class="section">
      <h2>机器人实例</h2>
      <p>暂无实例。（后续在此展示 /api/instances）</p>
    </section>

    <section class="section">
      <h2>插件</h2>
      <p>暂无插件。（后续在此展示 /api/plugins）</p>
    </section>

    <section class="section">
      <h2>日志</h2>
      <p>日志查看功能开发中。</p>
    </section>

    <div class="footer">
      每 5 秒自动刷新<template v-if="updatedAt"> · 上次更新 {{ updatedAt }}</template>
      <template v-if="error"> · {{ error }}</template>
    </div>
  </div>
</template>

<script setup>
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { api } from '../api'
import { notify } from '../toast'

const targets = ref([])
const loading = ref(false)
const busy = ref(false)

const form = ref({
  scope: '',
  senderType: 'group',
  sender: 'debug-user',
  target: 'debug-group',
  replyId: '',
  text: '/text'
})

const replies = ref([])
const injected = ref(null)
const error = ref('')
const polling = ref(false)

const presets = ['/text', '/markdown', '/card', '/list', '/reply', '/event', '/nothing']

let since = 0
let timer = null

/**
 * 注入范围：全部 / 单个插件 / 单个实例
 */
const scopeText = computed(() => {
  if (!injected.value) {
    return ''
  }

  if (injected.value.mode === 'plugin') {
    return '单个插件'
  }

  if (injected.value.mode === 'instance') {
    return '单个实例'
  }

  return '全部（插件 + 代理）'
})

/**
 * 拉组件和实例，拼注入范围下拉
 */
async function refresh() {
  loading.value = true

  try {
    const [components, instances] = await Promise.all([
      api.get('admin/components'),
      api.get('admin/instances')
    ])

    const plugins = (components.data || [])
      .filter(item => item.type === 'Plugin' && item.loaded)
      .map(item => ({ value: 'plugin:' + item.id, label: '插件：' + (item.name || item.id) }))

    const items = (instances.data || [])
      .map(item => ({ value: 'instance:' + item.uuid, label: '实例：' + (item.name || '未命名') + ' · ' + item.proxy }))

    targets.value = [{ value: '', label: '全部（插件 + 代理）' }, ...plugins, ...items]
  } catch (e) {
    error.value = e.message
  } finally {
    loading.value = false
  }
}

/**
 * 注入一条消息
 */
async function send() {
  if (!form.value.text.trim()) {
    notify('消息内容不能为空', 'bad')
    return
  }

  busy.value = true
  replies.value = []
  injected.value = null
  error.value = ''

  stopPolling()

  try {
    const res = await api.post('admin/debug/message', form.value)
    const data = res.data || {}

    injected.value = data.injected || null
    since = data.since || 0

    replies.value = (data.replies || []).map(item => ({ ...item, source: '同步' }))

    startPolling()
  } catch (e) {
    error.value = e.message
  } finally {
    busy.value = false
  }
}

/**
 * 每 500ms 拉一次异步回复，最多 20 秒
 */
function startPolling() {
  polling.value = true

  let times = 0

  timer = setInterval(async () => {
    times++

    try {
      const res = await api.get('admin/debug/replies?since=' + since)
      const items = res.data?.replies || []

      for (const item of items) {
        since = Math.max(since, item.seq)
        replies.value.push({ seq: item.seq, type: item.type, text: item.text, previewMime: item.previewMime, source: '异步 ' + item.time })
      }
    } catch {
      // 轮询失败不打扰用户
    }

    if (times >= 40) {
      stopPolling()
    }
  }, 500)
}

function stopPolling() {
  if (timer) {
    clearInterval(timer)
    timer = null
  }

  polling.value = false
}

/**
 * 点击快捷命令，直接填进输入框
 */
function usePreset(text) {
  form.value.text = text
}

onMounted(refresh)
onBeforeUnmount(stopPolling)
</script>

<template>
  <section class="section">
    <div class="toolbar align-end">
      <div class="field grow">
        <label>注入范围</label>
        <select v-model="form.scope" :disabled="loading">
          <option v-for="item in targets" :key="item.value" :value="item.value">
            {{ item.label }}
          </option>
        </select>
      </div>

      <div class="field">
        <label>会话类型</label>
        <select v-model="form.senderType">
          <option value="group">群聊 group</option>
          <option value="user">私聊 user</option>
        </select>
      </div>

      <div class="field">
        <label>发送者</label>
        <input v-model="form.sender" placeholder="debug-user" />
      </div>

      <div class="field">
        <label>回复目标</label>
        <input v-model="form.target" placeholder="debug-group" />
      </div>

      <div class="field">
        <label>消息 id（可选）</label>
        <input v-model="form.replyId" placeholder="留空自动生成" />
      </div>
    </div>

    <div class="field">
      <label>消息内容</label>
      <textarea v-model="form.text" rows="4" placeholder="输入要注入给插件的文本"></textarea>
    </div>

    <div class="toolbar" style="margin-top: 10px">
      <button v-for="item in presets" :key="item" class="btn ghost sm" @click="usePreset(item)">
        {{ item }}
      </button>

      <span class="spacer" />

      <button class="btn primary" :disabled="busy" @click="send">
        {{ busy ? '注入中…' : '注入消息' }}
      </button>
    </div>

    <p class="hint-line">
      默认走 <b>Dispatch</b>：消息按代理收到消息的方式分发给所有订阅者（插件处理、代理转发），回复不会真的发到平台。选了插件或实例就只发给它。
    </p>

    <p v-if="error" class="error">{{ error }}</p>

    <div v-if="injected" class="deps">
      <span class="dep state-dep">{{ scopeText }}</span>
      <span class="dep state-dep">发送者 {{ injected.sender }}</span>
      <span class="dep state-dep">目标 {{ injected.target }}</span>
      <span class="dep state-dep">消息 id {{ injected.replyId }}</span>
    </div>

    <p v-if="polling" class="hint-line">等待异步回复中…</p>

    <p v-if="replies.length === 0" class="empty">还没有回复，注入一条试试</p>

    <table v-else class="table">
      <thead>
        <tr>
          <th>#</th>
          <th>来源</th>
          <th>类型</th>
          <th>内容</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="(item, index) in replies" :key="index">
          <td class="meta">{{ index + 1 }}</td>
          <td class="meta">{{ item.source }}</td>
          <td><span class="tag">{{ item.type }}</span></td>
          <td class="mono">
            <div>{{ item.text }}</div>
            <img v-if="item.previewMime" :src="'/api/admin/debug/preview?seq=' + item.seq" class="preview" alt="预览" />
          </td>
        </tr>
      </tbody>
    </table>
  </section>
</template>

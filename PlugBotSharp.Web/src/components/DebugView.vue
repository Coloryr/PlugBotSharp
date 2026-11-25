<script setup>
import { computed, nextTick, onBeforeUnmount, onMounted, ref } from 'vue'
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
  text: ''
})

const messages = ref([])
const error = ref('')
const polling = ref(false)

const listRef = ref(null)
const presets = ['/text', '/markdown', '/card', '/list', '/reply', '/event', '/nothing']

let since = 0
let timer = null

const scopeLabel = computed(() => {
  const item = targets.value.find(x => x.value === form.value.scope)

  return item ? item.label : '全部（插件 + 代理）'
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
 * 注入一条消息，回复按聊天气泡追加
 */
async function send() {
  const text = form.value.text.trim()

  if (!text) {
    return
  }

  busy.value = true
  error.value = ''

  stopPolling()

  // 先把自己发的那条放上去，再等回复
  messages.value.push({
    role: 'user',
    type: form.value.senderType === 'group' ? '群聊' : '私聊',
    text: text,
    time: now()
  })

  form.value.text = ''

  await scrollToBottom()

  try {
    const res = await api.post('admin/debug/message', { ...form.value, text })
    const data = res.data || {}

    since = data.since || 0

    for (const item of data.replies || []) {
      pushReply(item, '同步')
    }

    startPolling()
  } catch (e) {
    error.value = e.message
  } finally {
    busy.value = false
  }
}

/**
 * 追加一条回复气泡
 */
function pushReply(item, source, time) {
  messages.value.push({
    role: 'reply',
    type: item.type,
    text: item.text,
    seq: item.seq,
    previewMime: item.previewMime,
    source: item.source || source,
    time: time || now()
  })

  scrollToBottom()
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
        pushReply(
          { type: item.type, text: item.text, seq: item.seq, previewMime: item.previewMime, source: item.source },
          '异步',
          item.time
        )
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
 * 清空对话，顺便清掉宿主那边的调试记录
 */
async function clearChat() {
  stopPolling()
  messages.value = []
  since = 0

  try {
    await api.post('admin/debug/reset')
  } catch {
    // 清空失败不影响界面
  }
}

/**
 * 滚到底部
 */
async function scrollToBottom() {
  await nextTick()

  if (listRef.value) {
    listRef.value.scrollTop = listRef.value.scrollHeight
  }
}

/**
 * 快捷命令
 */
function usePreset(text) {
  form.value.text = text
}

function now() {
  return new Date().toLocaleTimeString()
}

/**
 * Enter 发送，Shift+Enter 换行
 */
function onEnter(e) {
  if (e.shiftKey) {
    return
  }

  e.preventDefault()
  send()
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
          <option value="group">群聊</option>
          <option value="user">私聊</option>
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

      <button class="btn ghost" :disabled="messages.length === 0" @click="clearChat">清空对话</button>
    </div>

    <div ref="listRef" class="chat">
      <p v-if="messages.length === 0" class="empty">
        输入一条消息开始调试，比如 <code>/text</code>，Enter 发送
      </p>

      <div v-for="(item, index) in messages" :key="index" class="msg" :class="item.role">
        <div class="bubble">
          <div class="msg-meta">
            {{ item.role === 'user' ? '我' : (item.source || '回复') }}
            <span v-if="item.role === 'reply'" class="tag msg-type">{{ item.type }}</span>
            <span class="msg-time">{{ item.time }}</span>
          </div>

          <div class="msg-text">{{ item.text }}</div>

          <img
            v-if="item.previewMime"
            :src="'/api/admin/debug/preview?seq=' + item.seq"
            class="preview"
            alt="预览"
          />
        </div>
      </div>

      <p v-if="polling" class="msg-meta chat-tip">等待异步回复…</p>
    </div>

    <p v-if="error" class="error" style="margin-top: 10px">{{ error }}</p>

    <div class="chat-input">
      <textarea
        v-model="form.text"
        rows="2"
        placeholder="输入消息，Enter 发送，Shift+Enter 换行"
        @keydown.enter="onEnter"
      ></textarea>

      <button class="btn primary" :disabled="busy || !form.text.trim()" @click="send">
        {{ busy ? '发送中…' : '发送' }}
      </button>
    </div>

    <div class="chips">
      <button v-for="item in presets" :key="item" class="btn ghost sm" @click="usePreset(item)">
        {{ item }}
      </button>

      <span class="spacer" />

      <span class="meta">{{ scopeLabel }} · 回复不会发到平台</span>
    </div>
  </section>
</template>

<script setup>
import { onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import { api } from '../api'
import { auth, clearAuth } from '../auth'
import { notify } from '../toast'
import PasswordDialog from './PasswordDialog.vue'

const accounts = ref([])
const sessions = ref([])
const loading = ref(false)
const busy = ref('')
const error = ref('')

const addForm = reactive({ user: '', password: '' })
const dialog = reactive({ open: false, account: null, self: false })

const shortAgent = value => {
  if (!value) {
    return '-'
  }

  return value.length > 60 ? value.slice(0, 60) + '…' : value
}

/**
 * 拉取账户和登录会话
 */
async function refresh() {
  loading.value = true

  try {
    const [accountRes, sessionRes] = await Promise.all([
      api.get('admin/accounts'),
      api.get('admin/sessions')
    ])

    accounts.value = accountRes.data || []
    sessions.value = sessionRes.data || []
    error.value = ''
  } catch (e) {
    error.value = e.message
  } finally {
    loading.value = false
  }
}

/**
 * 新建账户
 */
async function addAccount() {
  if (!addForm.user.trim() || !addForm.password) {
    notify('请输入用户名和密码', 'warn')
    return
  }

  busy.value = 'add'

  try {
    const res = await api.post('admin/accounts', {
      user: addForm.user.trim(),
      password: addForm.password
    })

    notify(res.message || '账户已添加', 'ok')
    addForm.user = ''
    addForm.password = ''
  } catch (e) {
    notify('添加失败：' + e.message, 'bad')
  } finally {
    busy.value = ''
    await refresh()
  }
}

/**
 * 删除账户
 * @param {object} account 账户
 */
async function removeAccount(account) {
  if (!confirm(`确定删除账户 ${account.user}？`)) {
    return
  }

  busy.value = account.id + ':remove'

  try {
    const res = await api.del(`admin/accounts/${account.id}`)
    notify(res.message || '账户已删除', 'ok')
  } catch (e) {
    notify('删除失败：' + e.message, 'bad')
  } finally {
    busy.value = ''
    await refresh()
  }
}

/**
 * 打开改密弹窗
 * @param {object} account 账户
 */
function openPassword(account) {
  dialog.account = account
  dialog.self = account.self
  dialog.open = true
}

/**
 * 撤销一个会话
 * @param {object} session 会话
 */
async function revoke(session) {
  if (session.current && !confirm('这是当前登录的会话，撤销后会退出登录，继续？')) {
    return
  }

  busy.value = session.id

  try {
    const res = await api.del(`admin/sessions/${session.id}`)
    notify(res.message || '会话已撤销', 'ok')

    if (session.current) {
      clearAuth()
      return
    }
  } catch (e) {
    notify('撤销失败：' + e.message, 'bad')
  } finally {
    busy.value = ''
    await refresh()
  }
}

/**
 * 撤销其他全部会话
 */
async function revokeOthers() {
  if (!confirm('撤销除当前会话以外的全部登录会话？')) {
    return
  }

  busy.value = 'others'

  try {
    const res = await api.del('admin/sessions')
    notify(res.message || '其他会话已撤销', 'ok')
  } catch (e) {
    notify('撤销失败：' + e.message, 'bad')
  } finally {
    busy.value = ''
    await refresh()
  }
}

let timer = null

onMounted(() => {
  refresh()
  timer = setInterval(refresh, 10000)
})

onBeforeUnmount(() => {
  if (timer) {
    clearInterval(timer)
  }
})
</script>

<template>
  <section class="section">
    <div class="toolbar align-end">
      <div class="field grow">
        <label>用户名</label>
        <input v-model="addForm.user" placeholder="3-32 位字母、数字、下划线、点、横线" @keyup.enter="addAccount" />
      </div>

      <div class="field grow">
        <label>密码</label>
        <input v-model="addForm.password" type="password" placeholder="至少 8 位" @keyup.enter="addAccount" />
      </div>

      <button class="btn primary" :disabled="busy === 'add'" @click="addAccount">添加账户</button>
    </div>

    <p v-if="error" class="error">{{ error }}</p>

    <table class="table">
      <thead>
        <tr>
          <th>账户</th>
          <th>创建时间</th>
          <th>最后登录</th>
          <th>会话</th>
          <th class="right">操作</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="item in accounts" :key="item.id">
          <td>
            <div class="name">
              {{ item.user }}
              <span v-if="item.self" class="badge-soft">当前</span>
            </div>
          </td>
          <td class="mono">{{ item.created || '-' }}</td>
          <td class="mono">{{ item.lastLogin || '从未登录' }}</td>
          <td>{{ item.sessions }}</td>
          <td class="actions">
            <button class="btn" :disabled="!!busy" @click="openPassword(item)">改密</button>
            <button class="btn danger" :disabled="!!busy || item.self" @click="removeAccount(item)">删除</button>
          </td>
        </tr>
      </tbody>
    </table>
  </section>

  <section class="section">
    <div class="toolbar">
      <h2 class="section-title">登录会话</h2>
      <span class="spacer" />
      <button class="btn" :disabled="!!busy" @click="refresh">刷新</button>
      <button class="btn" :disabled="!!busy || sessions.length <= 1" @click="revokeOthers">撤销其他会话</button>
    </div>

    <div v-if="sessions.length === 0" class="empty">{{ loading ? '加载中…' : '没有登录会话' }}</div>

    <table v-else class="table">
      <thead>
        <tr>
          <th>账户</th>
          <th>IP</th>
          <th>设备</th>
          <th>登录时间</th>
          <th>最后活跃</th>
          <th class="right">操作</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="item in sessions" :key="item.id">
          <td>
            <div class="name">
              {{ item.user }}
              <span v-if="item.current" class="badge-soft">当前</span>
            </div>
          </td>
          <td class="mono">{{ item.ip || '-' }}</td>
          <td class="agent" :title="item.agent">{{ shortAgent(item.agent) }}</td>
          <td class="mono">{{ item.created }}</td>
          <td class="mono">{{ item.lastSeen }}</td>
          <td class="actions">
            <button class="btn danger" :disabled="!!busy" @click="revoke(item)">撤销</button>
          </td>
        </tr>
      </tbody>
    </table>
  </section>

  <PasswordDialog
    :open="dialog.open"
    :account="dialog.account"
    :self="dialog.self"
    @close="dialog.open = false"
    @done="refresh"
  />
</template>

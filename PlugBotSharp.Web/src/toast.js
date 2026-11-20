import { ref } from 'vue'

const toasts = ref([])
let seed = 0

/**
 * 弹一条提示，4 秒后自动消失
 * @param {string} message 内容
 * @param {'ok'|'bad'|'warn'|'info'} type 类型
 */
export function notify(message, type = 'info') {
  const id = ++seed
  toasts.value.push({ id, message, type })

  setTimeout(() => {
    toasts.value = toasts.value.filter(x => x.id !== id)
  }, 4000)
}

export { toasts }

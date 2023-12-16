import { useCallback } from 'react'
import { useHttpRequest } from './useHttpRequest'
import { BarMessage } from '..'

export const useDummyDataGenerator = (setErrorMessages: (msgs: BarMessage[]) => void) => {
  const { post } = useHttpRequest()

  return useCallback(async () => {
    let hasError = false

    return hasError
  }, [post, setErrorMessages])
}
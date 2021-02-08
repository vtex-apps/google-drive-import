/* eslint-disable padding-line-between-statements */
/* eslint-disable no-console */
import React, { FC, useState, useEffect } from 'react'
import { useRuntime } from 'vtex.render-runtime'
import axios from 'axios'
import {
  Layout,
  PageHeader,
  Card,
  Button,
  ButtonPlain,
  Divider,
  Spinner,
} from 'vtex.styleguide'
import { injectIntl, FormattedMessage, WrappedComponentProps } from 'react-intl'

import styles from './styles.css'
import GoogleSignIn from '../public/metadata/google_signin.png'

const CHECK_URL = '/google-drive-import/have-token'
const EMAIL_URL = '/google-drive-import/owner-email'
const FETCH_URL = '/google-drive-import/import-images'
const REVOKE_URL = '/google-drive-import/revoke-token'
const AUTH_URL = '/google-drive-import/auth'
const CREATE_SHEET_URL = '/google-drive-import/create-sheet'
const PROCESS_SHEET_URL = '/google-drive-import/sheet-import'

let initialCheck = false

const Admin: FC<WrappedComponentProps> = ({ intl }) => {
  const [state, setState] = useState<any>({
    fetching: false,
    revoking: false,
    fetched: false,
    authorization: false,
    email: null,
    loading: true,
    sheetProcessing: false,
    sheetProcessed: false,
    sheetCreating: false,
    sheetCreated: false,
  })
  const { account } = useRuntime()

  const {
    fetching,
    revoking,
    fetched,
    authorization,
    email,
    loading,
    sheetProcessing,
    sheetProcessed,
    sheetCreating,
    sheetCreated,
  } = state

  const fetch = () => {
    setState({
      ...state,
      fetching: true,
    })

    axios
      .get(FETCH_URL)
      .then((response: any) => {
        setState({
          ...state,
          fetching: false,
          fetched: response.data,
        })
        setTimeout(() => {
          setState({
            ...state,
            fetching: false,
            fetched: false,
          })
        }, 5000)
      })
      .catch(() => {
        setState({
          ...state,
          fetching: false,
          fetched: false,
        })
      })
  }

  const revoke = async () => {
    setState({
      ...state,
      revoking: true,
    })

    axios
      .get(REVOKE_URL)
      .then(() => {
        setState({
          ...state,
          revoking: false,
          authorization: false,
          email: null,
        })
      })
      .catch(() => {
        setState({
          ...state,
          revoking: false,
        })
      })

    return true
  }

  const auth = () => {
    revoke()
      .then(() => {
        window.top.location.href = AUTH_URL
      })
      .catch(() => {
        window.top.location.href = AUTH_URL
      })
  }

  const sheetImport = () => {
    setState({
      ...state,
      sheetProcessing: true,
    })

    axios
      .get(PROCESS_SHEET_URL)
      .then((response: any) => {
        setState({
          ...state,
          sheetProcessing: false,
          sheetProcessed: response.data,
        })
        setTimeout(() => {
          setState({
            ...state,
            sheetProcessing: false,
            sheetProcessed: false,
          })
        }, 5000)
      })
      .catch(() => {
        setState({
          ...state,
          sheetProcessing: false,
          sheetProcessed: false,
        })
      })
  }

  const createSheet = () => {
    setState({
      ...state,
      sheetCreating: true,
    })

    axios
      .get(CREATE_SHEET_URL)
      .then((response: any) => {
        setState({
          ...state,
          sheetCreating: false,
          sheetCreated: response.data,
        })
        setTimeout(() => {
          setState({
            ...state,
            sheetCreating: false,
            sheetCreated: false,
          })
        }, 5000)
      })
      .catch(() => {
        setState({
          ...state,
          sheetCreating: false,
          sheetCreated: false,
        })
      })
  }

  useEffect(() => {
    if (initialCheck) return
    initialCheck = true
    let accountConnected = false
    axios
      .get(CHECK_URL)
      .then((response: any) => {
        accountConnected = response.data
        setState({
          ...state,
          loading: false,
          authorization: accountConnected,
        })
      })
      .catch(() => {
        setState({
          ...state,
          loading: false,
        })
      })
      .then(() => {
        if (!accountConnected) return
        axios.get(EMAIL_URL).then((response: any) => {
          setState({
            ...state,
            loading: false,
            authorization: accountConnected,
            email: response.data,
          })
        })
      })
  })

  return (
    <Layout
      pageHeader={
        <div className="flex justify-center">
          <div className="w-100 mw-reviews-header">
            <PageHeader
              title={intl.formatMessage({
                id: 'admin/google-drive-import.title',
              })}
            />
          </div>
        </div>
      }
      fullWidth
    >
      <Card>
        {authorization && (
          <div className="flex">
            <div className="w-60">
              <h3 className="heading-3 mt4 mb4">
                <FormattedMessage id="admin/google-drive-import.sku-images.title" />
              </h3>
              <p>
                <FormattedMessage
                  id="admin/google-drive-import.connected.text"
                  values={{ lineBreak: <br /> }}
                />
              </p>
              <pre className={`${styles.code}`}>
                <FormattedMessage
                  id="admin/google-drive-import.folder-structure"
                  values={{ lineBreak: <br />, account }}
                />
              </pre>
              <p>
                <FormattedMessage id="admin/google-drive-import.instructions-line-01" />
              </p>

              <table className={`${styles.borderCollapse} ba collapse w-100`}>
                <thead>
                  <tr>
                    <th />
                    <th className="pa4">Description</th>
                  </tr>
                </thead>
                <tbody>
                  <tr>
                    <th className="flex justify-left bt items-center pa4">
                      IdType
                    </th>
                    <td className="bt bl pa4">
                      <FormattedMessage id="admin/google-drive-import.instructions-description-IdType" />
                    </td>
                  </tr>
                  <tr className={`${styles.striped}`}>
                    <th className="flex justify-left bt pa4">Id</th>
                    <td className="bt bl pa4">
                      <FormattedMessage id="admin/google-drive-import.instructions-description-Id" />
                    </td>
                  </tr>
                  <tr>
                    <th className="flex justify-left bt items-center pa4">
                      ImageName
                    </th>
                    <td className="bt bl pa4">
                      <FormattedMessage id="admin/google-drive-import.instructions-description-ImageName" />
                    </td>
                  </tr>
                  <tr className={`${styles.striped}`}>
                    <th className="flex justify-left bt pa4">ImageLabel</th>
                    <td className="bt bl pa4">
                      <FormattedMessage id="admin/google-drive-import.instructions-description-ImageLabel" />
                    </td>
                  </tr>
                  <tr className={`${styles.striped}`}>
                    <th className="flex justify-left bt pa4">Main?</th>
                    <td className="bt bl pa4">
                      <FormattedMessage id="admin/google-drive-import.instructions-description-Main" />
                    </td>
                  </tr>
                  <tr className={`${styles.striped}`}>
                    <th className="flex justify-left bt pa4">Specification</th>
                    <td className="bt bl pa4">
                      <FormattedMessage id="admin/google-drive-import.instructions-description-Spec" />
                    </td>
                  </tr>
                </tbody>
              </table>
              <p>
                <strong>
                  <FormattedMessage
                    id="admin/google-drive-import.instructions-examples"
                    values={{ lineBreak: <br /> }}
                  />
                </strong>
              </p>
              <p>
                <FormattedMessage id="admin/google-drive-import.instructions-line-02" />
              </p>
              <p>
                <FormattedMessage id="admin/google-drive-import.instructions-line-03" />
              </p>
            </div>
            <div
              style={{ flexGrow: 1 }}
              className="flex items-stretch w-10 justify-center"
            >
              <Divider orientation="vertical" />
            </div>
            <div className="w-30">
              {email && (
                <p>
                  <FormattedMessage id="admin/google-drive-import.connected-as" />{' '}
                  <strong>{`${email}`}</strong>
                </p>
              )}
              <div className="mt4 mb4">
                <Button
                  variation="danger-tertiary"
                  size="regular"
                  isLoading={revoking}
                  onClick={() => {
                    revoke()
                  }}
                  collapseLeft
                >
                  <FormattedMessage id="admin/google-drive-import.disconnect.button" />
                </Button>
              </div>
              <Button
                variation="primary"
                collapseLeft
                isLoading={fetching}
                onClick={() => {
                  fetch()
                }}
              >
                <FormattedMessage id="admin/google-drive-import.fetch.button" />
              </Button>
              {!fetching && fetched && (
                <p>
                  <strong>{`${fetched}`}</strong>
                </p>
              )}
              <div className="mv8">
                <Divider orientation="horizontal" />
              </div>
              <div className="flex-row">
                <div className="flex-col w-100">
                  <Button
                    variation="primary"
                    collapseLeft
                    isLoading={sheetCreating}
                    onClick={() => {
                      createSheet()
                    }}
                  >
                    <FormattedMessage id="admin/google-drive-import.create-sheet.button" />
                  </Button>
                  {!sheetCreating && sheetCreated && (
                    <p>
                      <strong>{`${sheetCreated}`}</strong>
                    </p>
                  )}
                </div>
                <div className="flex-col w-100 mt4">
                  <Button
                    variation="primary"
                    collapseLeft
                    isLoading={sheetProcessing}
                    onClick={() => {
                      sheetImport()
                    }}
                  >
                    <FormattedMessage id="admin/google-drive-import.sheet-import.button" />
                  </Button>
                  {!sheetProcessing && sheetProcessed && (
                    <p>
                      <strong>{`${sheetProcessed}`}</strong>
                    </p>
                  )}
                </div>
              </div>
            </div>
          </div>
        )}

        {!authorization && (
          <div>
            {loading && (
              <div className="pv6">
                <Spinner />
              </div>
            )}
            {!loading && (
              <div>
                <h2>
                  <FormattedMessage id="admin/google-drive-import.setup.title" />
                </h2>
                <p>
                  <FormattedMessage id="admin/google-drive-import.setup.description" />{' '}
                  <div className="mt4">
                    <ButtonPlain
                      variation="primary"
                      collapseLeft
                      onClick={() => {
                        auth()
                      }}
                    >
                      <img src={GoogleSignIn} alt="Sign in with Google" />
                    </ButtonPlain>
                  </div>
                </p>
              </div>
            )}
          </div>
        )}
      </Card>
    </Layout>
  )
}

export default injectIntl(Admin)

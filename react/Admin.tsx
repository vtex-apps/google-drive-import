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
  Divider,
  Spinner,
} from 'vtex.styleguide'
import { injectIntl, FormattedMessage, WrappedComponentProps } from 'react-intl'

const CHECK_URL = '/google-drive-import/have-token'
const EMAIL_URL = '/google-drive-import/owner-email'
const FETCH_URL = '/google-drive-import/import-images'
const REVOKE_URL = '/google-drive-import/revoke-token'

let initialCheck = false

const Admin: FC<WrappedComponentProps> = ({ intl }) => {
  const [state, setState] = useState<any>({
    fetching: false,
    revoking: false,
    fetched: false,
    authorization: false,
    email: null,
    loading: true,
  })
  const { account } = useRuntime()

  const { fetching, revoking, fetched, authorization, email, loading } = state

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

  const revoke = () => {
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
          authorizarion: false,
          email: null,
        })
      })
      .catch(() => {
        setState({
          ...state,
          revoking: false,
        })
      })
  }

  useEffect(() => {
    if (!initialCheck) {
      initialCheck = true
      axios
        .get(CHECK_URL)
        .then(() => {
          setState({
            ...state,
            loading: false,
            authorization: true,
          })
        })
        .catch(() => {
          setState({
            ...state,
            loading: false,
          })
        })
        .then(() => {
          axios.get(EMAIL_URL).then((response: any) => {
            setState({
              ...state,
              email: response.data,
            })
          })
        })
    }
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
            <div className="w-40">
              <h3 className="heading-3 mt4 mb4">
                <FormattedMessage id="admin/google-drive-import.sku-images.title" />
              </h3>
              <p>
                <FormattedMessage
                  id="admin/google-drive-import.connected.text"
                  values={{ lineBreak: <br /> }}
                />
              </p>
              <pre>
                <FormattedMessage
                  id="admin/google-drive-import.folder-structure"
                  values={{ lineBreak: <br />, account }}
                />
              </pre>
              <p>
                <FormattedMessage
                  id="admin/google-drive-import.instructions"
                  values={{
                    lineBreak: <br />,
                    filenameFormat: (
                      <b>
                        <FormattedMessage id="admin/google-drive-import.filename-format" />
                      </b>
                    ),
                  }}
                />
              </p>
              <div className="mt4">
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
              </div>
              {!fetching && fetched && <p>{`${fetched}`}</p>}
            </div>
            <div
              style={{ flexGrow: 1 }}
              className="flex items-stretch w-20 justify-center"
            >
              <Divider orientation="vertical" />
            </div>
            <div className="w-40">
              {email && (
                <p>
                  <FormattedMessage id="admin/google-drive-import.connected-as" />{' '}
                  <strong>{`${email}`}</strong>
                </p>
              )}
              <div className="mt4">
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
                    <Button
                      variation="primary"
                      collapseLeft
                      href="/google-drive-import/auth"
                      target="_top"
                    >
                      <FormattedMessage id="admin/google-drive-import.setup.button" />
                    </Button>
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

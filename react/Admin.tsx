/* eslint-disable @typescript-eslint/no-explicit-any */
import React, { FC, useState, useEffect } from 'react'
import { useRuntime } from 'vtex.render-runtime'
import axios from 'axios'
import {
  Layout,
  PageHeader,
  Card,
  Button,
  ButtonPlain,
  Spinner,
  Divider,
  Tabs,
  Tab,
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
const ADD_IMAGES_TO_SHEET_URL = '/google-drive-import/add-images-to-sheet'
const SHEET_LINK_URL = '/google-drive-import/sheet-link'
// const CLEAR_SHEET_URL = '/google-drive-import/clear-sheet'

let initialCheck = false
let SHEET_URL: any = null

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
    addingImages: false,
    imagesAdded: false,
    sheetUrl: SHEET_URL,
    currentTab: 1,
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
    addingImages,
    imagesAdded,
    currentTab,
    sheetUrl,
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

  const getSheetUrl = () => {
    axios
      .get(SHEET_LINK_URL, {
        withCredentials: true,
      })
      .then((response: any) => {
        SHEET_URL = response.data
        setState({
          ...state,
          sheetCreating: false,
          sheetUrl: response.data,
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
        getSheetUrl()
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

  const addImages = () => {
    setState({
      ...state,
      addingImages: true,
    })

    axios
      .get(ADD_IMAGES_TO_SHEET_URL)
      .then((response: any) => {
        setState({
          ...state,
          addingImages: false,
          imagesAdded: response.data,
        })
        setTimeout(() => {
          setState({
            ...state,
            addingImages: false,
            imagesAdded: false,
          })
        }, 5000)
      })
      .catch(() => {
        setState({
          ...state,
          addingImages: false,
          imagesAdded: false,
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
          sheetUrl: SHEET_URL,
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
            sheetUrl: SHEET_URL,
            authorization: accountConnected,
            email: response.data,
          })
        })
      })
    getSheetUrl()
  })

  const changeTab = (tab: number) => {
    setState({
      ...state,
      currentTab: tab,
    })
  }

  return (
    <Layout
      pageHeader={
        <div className="flex justify-center">
          <div className="w-100 mw-reviews-header">
            <PageHeader
              title={intl.formatMessage({
                id: 'admin/google-drive-import.title',
              })}
            >
              {authorization && (
                <div>
                  {email && (
                    <p>
                      <FormattedMessage id="admin/google-drive-import.connected-as" />{' '}
                      <strong>{`${email}`}</strong>
                    </p>
                  )}
                  <div className="mt4 mb4 tr">
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
              )}
            </PageHeader>
          </div>
        </div>
      }
      fullWidth
    >
      {!authorization && (
        <div>
          {loading && (
            <div className="pv6">
              <Spinner />
            </div>
          )}
          {!loading && (
            <div>
              <Card>
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
              </Card>
            </div>
          )}
        </div>
      )}
      {authorization && (
        <Tabs fullWidth>
          <Tab
            label="Instructions"
            active={currentTab === 1}
            onClick={() => changeTab(1)}
          >
            <div>
              <Card>
                {authorization && (
                  <div className="flex">
                    <div className="w-100">
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
                        There are two ways to associate images to SKUs:{' '}
                        <strong>
                          <a href="#skuimages" className="link black-90">
                            Standardized Naming
                          </a>
                        </strong>{' '}
                        (SKU Images) and{' '}
                        <strong>
                          <a href="#spreadsheet" className="link black-90">
                            Spreadsheet
                          </a>
                        </strong>
                      </p>
                      <Divider />
                      <h2 className="heading-3 mt4 mb4" id="skuimages">
                        <FormattedMessage id="admin/google-drive-import.sku-images.title" />
                      </h2>
                      <p>
                        <FormattedMessage id="admin/google-drive-import.instructions-line-01" />
                      </p>

                      <table
                        className={`${styles.borderCollapse} ba collapse w-100`}
                      >
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
                            <th className="flex justify-left bt pa4">
                              ImageLabel
                            </th>
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
                            <th className="flex justify-left bt pa4">
                              Specification
                            </th>
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

                      <Divider />
                      <h2 id="spreadsheet">Spreadsheet</h2>
                      <p>Spreadsheet instructions here</p>
                    </div>
                  </div>
                )}
              </Card>
            </div>
          </Tab>
          <Tab
            label="Actions"
            active={currentTab === 2}
            onClick={() => changeTab(2)}
          >
            <div className="bg-base pa8">
              <h2>SKU Images</h2>
              <Card>
                <div className="flex">
                  <div className="w-70">
                    <p>
                      The App fetches new images automatically, but you can
                      force it to fetch new images immediately
                    </p>
                  </div>

                  <div
                    style={{ flexGrow: 1 }}
                    className="flex items-stretch w-20 justify-center"
                  >
                    <Divider orientation="vertical" />
                  </div>

                  <div className="w-30 items-center flex">
                    {!fetched && (
                      <Button
                        variation="primary"
                        collapseLeft
                        block
                        isLoading={fetching}
                        onClick={() => {
                          fetch()
                        }}
                      >
                        <FormattedMessage id="admin/google-drive-import.fetch.button" />
                      </Button>
                    )}

                    {!fetching && fetched && (
                      <p className="block">
                        <strong>{`${fetched}`}</strong>
                      </p>
                    )}
                  </div>
                </div>
              </Card>
              <br />
              <h2>Spreadsheet</h2>
              {!sheetUrl && (
                <Card>
                  <div className="flex">
                    <div className="w-70">
                      <p>
                        Creates a Sheet with a default structure that you need
                        for the mapping
                      </p>
                    </div>

                    <div
                      style={{ flexGrow: 1 }}
                      className="flex items-stretch w-20 justify-center"
                    >
                      <Divider orientation="vertical" />
                    </div>

                    <div className="w-30 items-center flex">
                      <Button
                        variation="primary"
                        collapseLeft
                        block
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
                  </div>
                </Card>
              )}
              {sheetUrl && (
                <Card>
                  <div className="flex">
                    <div className="w-100">
                      <p>
                        Access the mapping Spreadsheet{' '}
                        <a href={sheetUrl} target="_blank" rel="noreferrer">
                          {sheetUrl}
                        </a>
                      </p>
                    </div>
                  </div>
                </Card>
              )}
              <br />
              <Card>
                <div className="flex">
                  <div className="w-70">
                    <p>
                      Starts the image importing process based on the mapping
                      defined at the Spreadsheet
                    </p>
                  </div>
                  <div
                    style={{ flexGrow: 1 }}
                    className="flex items-stretch w-20 justify-center"
                  >
                    <Divider orientation="vertical" />
                  </div>
                  <div className="w-30 items-center flex">
                    {!sheetProcessed && (
                      <Button
                        variation="primary"
                        collapseLeft
                        block
                        isLoading={sheetProcessing}
                        onClick={() => {
                          sheetImport()
                        }}
                      >
                        <FormattedMessage id="admin/google-drive-import.sheet-import.button" />
                      </Button>
                    )}
                    {!sheetProcessing && sheetProcessed && (
                      <p>
                        <strong>{`${sheetProcessed}`}</strong>
                      </p>
                    )}
                  </div>
                </div>
              </Card>
              <br />
              <Card>
                <div className="flex">
                  <div className="w-70">
                    <p>
                      Clears the Spreadsheet and fills the image names and
                      thumbnails automatically based on the files at the{' '}
                      <strong>NEW</strong> folder
                    </p>
                  </div>
                  <div
                    style={{ flexGrow: 1 }}
                    className="flex items-stretch w-20 justify-center"
                  >
                    <Divider orientation="vertical" />
                  </div>
                  <div className="w-30 items-center flex">
                    {!imagesAdded && (
                      <Button
                        variation="primary"
                        collapseLeft
                        block
                        isLoading={addingImages}
                        onClick={() => {
                          addImages()
                        }}
                      >
                        <FormattedMessage id="admin/google-drive-import.add-images.button" />
                      </Button>
                    )}
                    {!addingImages && imagesAdded && (
                      <p>
                        <strong>{`${imagesAdded}`}</strong>
                      </p>
                    )}
                  </div>
                </div>
              </Card>
            </div>
          </Tab>
        </Tabs>
      )}
    </Layout>
  )
}

export default injectIntl(Admin)

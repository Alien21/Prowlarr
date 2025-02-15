import _ from 'lodash';
import { createAction } from 'redux-actions';
import { filterTypePredicates, sortDirections } from 'Helpers/Props';
import createFetchHandler from 'Store/Actions/Creators/createFetchHandler';
import createRemoveItemHandler from 'Store/Actions/Creators/createRemoveItemHandler';
import createSaveProviderHandler, {
  createCancelSaveProviderHandler
} from 'Store/Actions/Creators/createSaveProviderHandler';
import createTestAllProvidersHandler from 'Store/Actions/Creators/createTestAllProvidersHandler';
import createTestProviderHandler, {
  createCancelTestProviderHandler
} from 'Store/Actions/Creators/createTestProviderHandler';
import createSetProviderFieldValueReducer from 'Store/Actions/Creators/Reducers/createSetProviderFieldValueReducer';
import createSetSettingValueReducer from 'Store/Actions/Creators/Reducers/createSetSettingValueReducer';
import { createThunk, handleThunks } from 'Store/thunks';
import dateFilterPredicate from 'Utilities/Date/dateFilterPredicate';
import getSectionState from 'Utilities/State/getSectionState';
import updateSectionState from 'Utilities/State/updateSectionState';
import translate from 'Utilities/String/translate';
import createBulkEditItemHandler from './Creators/createBulkEditItemHandler';
import createBulkRemoveItemHandler from './Creators/createBulkRemoveItemHandler';
import createHandleActions from './Creators/createHandleActions';
import createClearReducer from './Creators/Reducers/createClearReducer';
import createSetClientSideCollectionSortReducer from './Creators/Reducers/createSetClientSideCollectionSortReducer';

//
// Variables

export const section = 'indexers';
const schemaSection = `${section}.schema`;

//
// State

export const defaultState = {
  isFetching: false,
  isPopulated: false,
  error: null,
  isDeleting: false,
  deleteError: null,
  selectedSchema: {},
  isSaving: false,
  saveError: null,
  isTesting: false,
  isTestingAll: false,
  items: [],
  pendingChanges: {},

  schema: {
    isFetching: false,
    isPopulated: false,
    error: null,
    sortKey: 'sortName',
    sortDirection: sortDirections.ASCENDING,
    items: []
  }
};

export const filters = [
  {
    key: 'all',
    label: () => translate('All'),
    filters: []
  }
];

export const filterPredicates = {
  added: function(item, filterValue, type) {
    return dateFilterPredicate(item.added, filterValue, type);
  },

  vipExpiration: function(item, filterValue, type) {
    const vipExpiration =
    item.fields.find((field) => field.name === 'vipExpiration')?.value ?? null;

    return dateFilterPredicate(vipExpiration, filterValue, type);
  },

  categories: function(item, filterValue, type) {
    const predicate = filterTypePredicates[type];

    const { categories = [] } = item.capabilities || {};

    const categoryList = categories
      .filter((category) => category.id < 100000)
      .reduce((acc, element) => {
        acc.push(element.id);

        if (element.subCategories && element.subCategories.length > 0) {
          element.subCategories.forEach((subCat) => {
            acc.push(subCat.id);
          });
        }

        return acc;
      }, []);

    return predicate(categoryList, filterValue);
  }
};

export const sortPredicates = {
  status: function({ enable, redirect }) {
    let result = 0;

    if (redirect) {
      result++;
    }

    if (enable) {
      result += 2;
    }

    return result;
  },

  vipExpiration: function({ fields = [] }) {
    return fields.find((field) => field.name === 'vipExpiration')?.value ?? '';
  },

  minimumSeeders: function({ fields = [] }) {
    return fields.find((field) => field.name === 'torrentBaseSettings.appMinimumSeeders')?.value ?? undefined;
  },

  seedRatio: function({ fields = [] }) {
    return fields.find((field) => field.name === 'torrentBaseSettings.seedRatio')?.value ?? undefined;
  },

  seedTime: function({ fields = [] }) {
    return fields.find((field) => field.name === 'torrentBaseSettings.seedTime')?.value ?? undefined;
  },

  packSeedTime: function({ fields = [] }) {
    return fields.find((field) => field.name === 'torrentBaseSettings.packSeedTime')?.value ?? undefined;
  },

  preferMagnetUrl: function({ fields = [] }) {
    return fields.find((field) => field.name === 'torrentBaseSettings.preferMagnetUrl')?.value ?? undefined;
  }
};

//
// Actions Types

export const FETCH_INDEXERS = 'indexers/fetchIndexers';
export const FETCH_INDEXER_SCHEMA = 'indexers/fetchIndexerSchema';
export const SELECT_INDEXER_SCHEMA = 'indexers/selectIndexerSchema';
export const SET_INDEXER_SCHEMA_SORT = 'indexers/setIndexerSchemaSort';
export const CLEAR_INDEXER_SCHEMA = 'indexers/clearIndexerSchema';
export const CLONE_INDEXER = 'indexers/cloneIndexer';
export const SET_INDEXER_VALUE = 'indexers/setIndexerValue';
export const SET_INDEXER_FIELD_VALUE = 'indexers/setIndexerFieldValue';
export const SAVE_INDEXER = 'indexers/saveIndexer';
export const CANCEL_SAVE_INDEXER = 'indexers/cancelSaveIndexer';
export const DELETE_INDEXER = 'indexers/deleteIndexer';
export const TEST_INDEXER = 'indexers/testIndexer';
export const CANCEL_TEST_INDEXER = 'indexers/cancelTestIndexer';
export const TEST_ALL_INDEXERS = 'indexers/testAllIndexers';
export const BULK_EDIT_INDEXERS = 'indexers/bulkEditIndexers';
export const BULK_DELETE_INDEXERS = 'indexers/bulkDeleteIndexers';

//
// Action Creators

export const fetchIndexers = createThunk(FETCH_INDEXERS);
export const fetchIndexerSchema = createThunk(FETCH_INDEXER_SCHEMA);
export const selectIndexerSchema = createAction(SELECT_INDEXER_SCHEMA);
export const setIndexerSchemaSort = createAction(SET_INDEXER_SCHEMA_SORT);
export const clearIndexerSchema = createAction(CLEAR_INDEXER_SCHEMA);
export const cloneIndexer = createAction(CLONE_INDEXER);

export const saveIndexer = createThunk(SAVE_INDEXER);
export const cancelSaveIndexer = createThunk(CANCEL_SAVE_INDEXER);
export const deleteIndexer = createThunk(DELETE_INDEXER);
export const testIndexer = createThunk(TEST_INDEXER);
export const cancelTestIndexer = createThunk(CANCEL_TEST_INDEXER);
export const testAllIndexers = createThunk(TEST_ALL_INDEXERS);
export const bulkEditIndexers = createThunk(BULK_EDIT_INDEXERS);
export const bulkDeleteIndexers = createThunk(BULK_DELETE_INDEXERS);

export const setIndexerValue = createAction(SET_INDEXER_VALUE, (payload) => {
  return {
    section,
    ...payload
  };
});

export const setIndexerFieldValue = createAction(SET_INDEXER_FIELD_VALUE, (payload) => {
  return {
    section,
    ...payload
  };
});

//
// Action Handlers

function applySchemaDefaults(selectedSchema, schemaDefaults) {
  if (!schemaDefaults) {
    return selectedSchema;
  } else if (_.isFunction(schemaDefaults)) {
    return schemaDefaults(selectedSchema);
  }

  return Object.assign(selectedSchema, schemaDefaults);
}

function selectSchema(state, payload, schemaDefaults) {
  const newState = getSectionState(state, section);

  const {
    implementation,
    name
  } = payload;

  const selectedImplementation = _.find(newState.schema.items, { implementation, name });

  newState.selectedSchema = applySchemaDefaults(_.cloneDeep(selectedImplementation), schemaDefaults);

  return updateSectionState(state, section, newState);
}

export const actionHandlers = handleThunks({
  [FETCH_INDEXERS]: createFetchHandler(section, '/indexer'),
  [FETCH_INDEXER_SCHEMA]: createFetchHandler(schemaSection, '/indexer/schema'),

  [SAVE_INDEXER]: createSaveProviderHandler(section, '/indexer'),
  [CANCEL_SAVE_INDEXER]: createCancelSaveProviderHandler(section),
  [DELETE_INDEXER]: createRemoveItemHandler(section, '/indexer'),
  [TEST_INDEXER]: createTestProviderHandler(section, '/indexer'),
  [CANCEL_TEST_INDEXER]: createCancelTestProviderHandler(section),
  [TEST_ALL_INDEXERS]: createTestAllProvidersHandler(section, '/indexer'),
  [BULK_EDIT_INDEXERS]: createBulkEditItemHandler(section, '/indexer/bulk'),
  [BULK_DELETE_INDEXERS]: createBulkRemoveItemHandler(section, '/indexer/bulk')
});

//
// Reducers

export const reducers = createHandleActions({
  [SET_INDEXER_VALUE]: createSetSettingValueReducer(section),
  [SET_INDEXER_FIELD_VALUE]: createSetProviderFieldValueReducer(section),
  [SET_INDEXER_SCHEMA_SORT]: createSetClientSideCollectionSortReducer(schemaSection),

  [SELECT_INDEXER_SCHEMA]: (state, { payload }) => {
    return selectSchema(state, payload, (selectedSchema) => {
      selectedSchema.name = payload.name ?? payload.implementationName;
      selectedSchema.implementationName = payload.implementationName;
      selectedSchema.enable = selectedSchema.supportsRss;

      return selectedSchema;
    });
  },

  [CLEAR_INDEXER_SCHEMA]: createClearReducer(schemaSection, defaultState),

  [CLONE_INDEXER]: function(state, { payload }) {
    const id = payload.id;
    const newState = getSectionState(state, section);
    const item = newState.items.find((i) => i.id === id);

    // Use selectedSchema so `createProviderSettingsSelector` works properly
    const selectedSchema = { ...item };
    delete selectedSchema.id;
    delete selectedSchema.name;

    selectedSchema.fields = selectedSchema.fields.map((field) => {
      const newField = { ...field };

      if (newField.privacy === 'apiKey' || newField.privacy === 'password') {
        newField.value = '';
      }

      return newField;
    });

    newState.selectedSchema = selectedSchema;

    // Set the name in pendingChanges
    newState.pendingChanges = {
      name: translate('DefaultNameCopiedProfile', { name: item.name })
    };

    return updateSectionState(state, section, newState);
  }
}, defaultState, section);
